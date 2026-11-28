using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// One place the farm keeps money — cash in hand, a bank account or a mobile wallet. Every cash
/// posting (<see cref="FinancialTransaction"/> settled against Cash) belongs to exactly one money
/// account, so the balance of each is simply the sum of its postings' cash effects.
/// </summary>
/// <remarks>
/// Every farm has one default account ("Cash in hand"). Postings that do not name an account land
/// there, which keeps old callers (and old rows) correct. The balance sheet still shows a single
/// "Cash &amp; Bank" line: the accounts are a breakdown of it, never extra assets.
/// </remarks>
public sealed class MoneyAccount : AuditableEntity, IAggregateRoot
{
    public const string DefaultName = "Cash in hand";

    public Guid FarmId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public MoneyAccountKind Kind { get; private set; }

    /// <summary>Bank or wallet provider (e.g. "Sonali Bank", "bKash").</summary>
    public string? ProviderName { get; private set; }

    /// <summary>Account or wallet number.</summary>
    public string? AccountNumber { get; private set; }

    public string? Branch { get; private set; }
    public string? Notes { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    private MoneyAccount() { } // For EF Core

    public static MoneyAccount Create(
        Guid tenantId,
        Guid farmId,
        string name,
        MoneyAccountKind kind,
        string? providerName = null,
        string? accountNumber = null,
        string? branch = null,
        string? notes = null,
        bool isDefault = false)
    {
        if (farmId == Guid.Empty)
            throw new ArgumentException("Farm is required.", nameof(farmId));

        var account = new MoneyAccount
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            IsDefault = isDefault,
            IsActive = true,
        };
        account.SetDetails(name, kind, providerName, accountNumber, branch, notes);
        account.SetTenantId(tenantId);
        return account;
    }

    /// <summary>The account every farm starts with, where unassigned cash postings land.</summary>
    public static MoneyAccount CreateDefault(Guid tenantId, Guid farmId) =>
        Create(tenantId, farmId, DefaultName, MoneyAccountKind.Cash, isDefault: true);

    public void Update(string name, MoneyAccountKind kind, string? providerName, string? accountNumber, string? branch, string? notes) =>
        SetDetails(name, kind, providerName, accountNumber, branch, notes);

    /// <summary>Stops new money going through the account. Its history and balance stay.</summary>
    public void Deactivate()
    {
        if (IsDefault)
            throw new InvalidOperationException("The default money account cannot be closed. Make another account the default first.");
        IsActive = false;
    }

    public void Activate() => IsActive = true;

    /// <summary>Only the farm's money-account service may move the default flag (exactly one per farm).</summary>
    public void SetDefault(bool isDefault)
    {
        if (isDefault && !IsActive)
            throw new InvalidOperationException("A closed account cannot be the default. Reopen it first.");
        IsDefault = isDefault;
    }

    private void SetDetails(string name, MoneyAccountKind kind, string? providerName, string? accountNumber, string? branch, string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));
        if (!Enum.IsDefined(kind))
            throw new ArgumentException("Unknown account kind.", nameof(kind));

        Name = name.Trim();
        Kind = kind;
        ProviderName = Clean(providerName);
        AccountNumber = Clean(accountNumber);
        Branch = Clean(branch);
        Notes = Clean(notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
