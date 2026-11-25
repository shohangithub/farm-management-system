using System.Text;
using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Farm360.Persistence.Services;

/// <summary>
/// Which rows of the business database belong to one tenant, worked out from the EF Core model so
/// a table added later is covered without touching this code:
/// <list type="bullet">
///   <item>every table with a <c>TenantId</c> column: rows where <c>TenantId = @tenantId</c>
///         (the tenant query filter is not involved, this is plain SQL);</item>
///   <item>every table WITHOUT <c>TenantId</c> that has a foreign key into a table already covered
///         (e.g. weight records, order lines, role permissions): rows whose parent row belongs to the
///         tenant, followed transitively.</item>
/// </list>
/// Steps are ordered dependents-first along the foreign keys. The tenant row itself is not part
/// of the plan; the caller deletes it last.
/// </summary>
/// <remarks>
/// Table and column names come only from the model (never from input) and are bracket-quoted;
/// the tenant id is always a parameter (<c>@tenantId</c>).
/// </remarks>
internal sealed class TenantDeletePlan
{
    public const string TenantIdColumn = "TenantId";
    public const string TenantIdParameter = "@tenantId";

    private TenantDeletePlan(IReadOnlyList<Step> steps, IReadOnlyList<string> textScans)
    {
        Steps = steps;
        TextScans = textScans;
    }

    /// <summary>Delete steps, dependents first.</summary>
    public IReadOnlyList<Step> Steps { get; }

    /// <summary>
    /// For every table (all tenants, the tenants table included): a query of its text values ending
    /// in <c>WHERE v.Url IN </c>, to which the caller appends a parenthesised parameter list.
    /// </summary>
    public IReadOnlyList<string> TextScans { get; }

    /// <param name="Table">schema.table, for reports.</param>
    /// <param name="DeleteSql">DELETE of the tenant's rows.</param>
    /// <param name="CountSql">COUNT_BIG of the same rows.</param>
    /// <param name="UploadUrlSql">The tenant's string values that look like stored upload URLs, or null when the table has no text column.</param>
    /// <param name="ScopedByTenantId">True when the table has its own TenantId column.</param>
    public sealed record Step(string Table, string DeleteSql, string CountSql, string? UploadUrlSql, bool ScopedByTenantId);

    public static TenantDeletePlan Build(IModel model)
    {
        var relational = model.GetRelationalModel();
        var tenantType = model.FindEntityType(typeof(Tenant))
            ?? throw new InvalidOperationException("The tenant entity is not in the model.");
        var tenantTable = (tenantType.GetTableName() is { } name ? relational.FindTable(name, tenantType.GetSchema()) : null)
            ?? throw new InvalidOperationException("The tenants table is not in the model.");

        var tables = relational.Tables.Where(t => !ReferenceEquals(t, tenantTable)).ToList();

        // 1. Tables scoped by their own TenantId column.
        var scoped = tables.Where(t => t.FindColumn(TenantIdColumn) is not null).ToHashSet();

        // 2. Tables reached through a foreign key into a covered table (fixed point). Each one keeps
        //    only the keys into tables covered BEFORE it, so predicates never recurse in a cycle.
        var covered = new HashSet<ITable>(scoped);
        var viaParents = new Dictionary<ITable, List<IForeignKeyConstraint>>();
        bool added;
        do
        {
            added = false;
            foreach (var table in tables.Where(t => !covered.Contains(t)))
            {
                var keys = table.ForeignKeyConstraints
                    .Where(fk => !ReferenceEquals(fk.PrincipalTable, table) && covered.Contains(fk.PrincipalTable))
                    .ToList();
                if (keys.Count == 0)
                    continue;
                viaParents[table] = keys;
                added = true;
            }

            foreach (var table in viaParents.Keys)
                covered.Add(table);
        }
        while (added);

        var aliasCounter = 0;
        string NextAlias() => "t" + (aliasCounter++).ToString(System.Globalization.CultureInfo.InvariantCulture);

        string Predicate(ITable table, string alias)
        {
            if (scoped.Contains(table))
                return $"{alias}.{Quote(TenantIdColumn)} = {TenantIdParameter}";

            var parts = viaParents[table].Select(fk =>
            {
                var parentAlias = NextAlias();
                var join = string.Join(" AND ", fk.Columns.Zip(fk.PrincipalColumns,
                    (c, p) => $"{parentAlias}.{Quote(p.Name)} = {alias}.{Quote(c.Name)}"));
                return $"EXISTS (SELECT 1 FROM {QuoteTable(fk.PrincipalTable)} AS {parentAlias} WHERE {join} AND {Predicate(fk.PrincipalTable, parentAlias)})";
            });
            return "(" + string.Join(" OR ", parts) + ")";
        }

        var steps = new List<Step>();
        foreach (var table in Order(covered))
        {
            var alias = NextAlias();
            var where = Predicate(table, alias);
            var from = $"{QuoteTable(table)} AS {alias}";

            var textColumns = table.Columns
                .Where(c => c.ProviderClrType == typeof(string))
                .Select(c => $"({alias}.{Quote(c.Name)})")
                .ToList();
            string? uploads = null;
            if (textColumns.Count > 0)
            {
                uploads = new StringBuilder()
                    .Append("SELECT DISTINCT v.Url AS [Value] FROM ").Append(from)
                    .Append(" CROSS APPLY (VALUES ").Append(string.Join(", ", textColumns)).Append(") AS v(Url)")
                    .Append(" WHERE ").Append(where)
                    .Append(" AND (v.Url LIKE N'/uploads/%' OR v.Url LIKE N'uploads/%')")
                    .ToString();
            }

            steps.Add(new Step(
                Display(table),
                $"DELETE {alias} FROM {from} WHERE {where}",
                $"SELECT COUNT_BIG(*) AS [Value] FROM {from} WHERE {where}",
                uploads,
                scoped.Contains(table)));
        }

        var scans = relational.Tables
            .Select(t => (Table: t, Text: t.Columns.Where(c => c.ProviderClrType == typeof(string)).ToList()))
            .Where(x => x.Text.Count > 0)
            .Select(x =>
                $"SELECT DISTINCT v.Url AS [Value] FROM {QuoteTable(x.Table)} AS s CROSS APPLY (VALUES "
                + string.Join(", ", x.Text.Select(c => $"(s.{Quote(c.Name)})"))
                + ") AS v(Url) WHERE v.Url IN ")
            .ToList();

        return new TenantDeletePlan(steps, scans);
    }

    /// <summary>Dependents before their principals (Kahn); tables left in a cycle go last, the purge retries them.</summary>
    private static List<ITable> Order(HashSet<ITable> tables)
    {
        // parent -> children that reference it (within the set, ignoring self references)
        var children = tables.ToDictionary(t => t, _ => new HashSet<ITable>());
        foreach (var table in tables)
        {
            foreach (var fk in table.ForeignKeyConstraints)
            {
                if (!ReferenceEquals(fk.PrincipalTable, table) && children.TryGetValue(fk.PrincipalTable, out var set))
                    set.Add(table);
            }
        }

        var ordered = new List<ITable>();
        var remaining = new HashSet<ITable>(tables);
        while (remaining.Count > 0)
        {
            // A table can go once nothing still remaining references it.
            var ready = remaining
                .Where(t => !children[t].Any(remaining.Contains))
                .OrderBy(Display, StringComparer.Ordinal)
                .ToList();
            if (ready.Count == 0)
            {
                ordered.AddRange(remaining.OrderBy(Display, StringComparer.Ordinal));
                break;
            }

            foreach (var table in ready)
            {
                ordered.Add(table);
                remaining.Remove(table);
            }
        }

        return ordered;
    }

    private static string Display(ITable table) => (table.Schema is null ? "" : table.Schema + ".") + table.Name;

    private static string QuoteTable(ITable table) =>
        table.Schema is null ? Quote(table.Name) : $"{Quote(table.Schema)}.{Quote(table.Name)}";

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
