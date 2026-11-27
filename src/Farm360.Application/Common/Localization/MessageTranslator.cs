using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Farm360.Application.Common.Localization;

/// <summary>
/// Template-based translator for server messages (see <see cref="BanglaMessageCatalogue"/>).
/// <para>
/// Each catalogue entry is the English message with its interpolated values written as
/// <c>{name}</c>; the entry is turned into an anchored regular expression that captures those
/// values, and the Bangla text gets them back by name. A placeholder can carry a kind:
/// <c>{name:w}</c> looks the captured value up in <see cref="BanglaMessageCatalogue.Words"/>
/// (field names, statuses, record types, actions), <c>{name:t}</c> translates the captured value
/// as a message in its own right. Plain values (tags, item names, amounts) are kept as they are,
/// except that English month names in dates become Bangla ones.
/// </para>
/// <para>
/// When no template matches the whole message, a message made of several sentences is translated
/// sentence by sentence (longest matching run first). Anything still unmatched stays in English.
/// </para>
/// </summary>
public sealed class MessageTranslator : IMessageTranslator
{
    /// <summary>Language code of Bangla.</summary>
    public const string Bangla = "bn";

    private const int MaxDepth = 3;

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly Regex PlaceholderPattern = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::(?<kind>[wt]))?\}",
        RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex ParameterSuffix = new(
        @"\s*\(Parameter '[^']*'\)\s*$", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex SentenceBreak = new(
        @"(?<=[.!?])\s+(?=[A-Z'""{(])", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex EnglishDate = new(
        @"\b(?<day>\d{1,2})(?<sep>[ -])(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\k<sep>(?<year>\d{4})\b",
        RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Regex CollectionIndex = new(@"\[\d+\]", RegexOptions.CultureInvariant, MatchTimeout);

    private static readonly Dictionary<string, string> BanglaMonths = new(StringComparer.Ordinal)
    {
        ["Jan"] = "জানুয়ারি", ["Feb"] = "ফেব্রুয়ারি", ["Mar"] = "মার্চ", ["Apr"] = "এপ্রিল",
        ["May"] = "মে", ["Jun"] = "জুন", ["Jul"] = "জুলাই", ["Aug"] = "আগস্ট",
        ["Sep"] = "সেপ্টেম্বর", ["Oct"] = "অক্টোবর", ["Nov"] = "নভেম্বর", ["Dec"] = "ডিসেম্বর",
    };

    private static readonly Lazy<IReadOnlyList<Template>> BanglaTemplates = new(
        () => Compile(BanglaMessageCatalogue.Templates), LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly Lazy<IReadOnlyDictionary<string, string>> BanglaWords = new(
        () =>
        {
            // "TagId", "Tag Id" and "Tag ID" are one key; the first entry wins.
            var words = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (english, bangla) in BanglaMessageCatalogue.Words)
                words.TryAdd(NormalizeWord(english), bangla);
            return words;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Shared instance for callers outside dependency injection (middleware, tests).</summary>
    public static MessageTranslator Default { get; } = new();

    /// <summary>Number of Bangla templates in the catalogue.</summary>
    public static int TemplateCount => BanglaTemplates.Value.Count;

    /// <summary>True for "bn", "bn-BD" and other Bangla culture names.</summary>
    public static bool IsBangla(string? language) =>
        !string.IsNullOrEmpty(language)
        && (string.Equals(language, Bangla, StringComparison.OrdinalIgnoreCase)
            || language.StartsWith(Bangla + "-", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public string Translate(string message, string language)
    {
        if (string.IsNullOrWhiteSpace(message) || !IsBangla(language))
            return message;

        // ArgumentException appends " (Parameter 'x')"; the parameter name means nothing to a farmer.
        var text = ParameterSuffix.Replace(message.Trim(), string.Empty);
        return TranslateText(text, 0) ?? message;
    }

    /// <summary>A field or record name in Bangla (used for validation messages), or null when unknown.</summary>
    public static string? TranslateWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return null;
        return BanglaWords.Value.TryGetValue(NormalizeWord(word), out var bangla) ? bangla : null;
    }

    private static string? TranslateText(string text, int depth)
    {
        if (depth > MaxDepth || text.Length == 0)
            return null;

        var whole = MatchTemplate(text, depth);
        if (whole is not null)
            return whole;

        string[] sentences;
        try
        {
            sentences = SentenceBreak.Split(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (sentences.Length < 2)
            return null;

        var parts = new List<string>(sentences.Length);
        var translatedAny = false;
        var i = 0;
        while (i < sentences.Length)
        {
            string? hit = null;
            var end = i + 1;
            for (var j = sentences.Length; j > i; j--)
            {
                if (i == 0 && j == sentences.Length)
                    continue; // the whole message was tried above

                hit = MatchTemplate(string.Join(' ', sentences[i..j]), depth);
                if (hit is not null)
                {
                    end = j;
                    break;
                }
            }

            if (hit is null)
            {
                parts.Add(sentences[i]);
                i++;
            }
            else
            {
                parts.Add(hit);
                translatedAny = true;
                i = end;
            }
        }

        return translatedAny ? string.Join(' ', parts) : null;
    }

    private static string? MatchTemplate(string text, int depth)
    {
        foreach (var template in BanglaTemplates.Value)
        {
            Match match;
            try
            {
                match = template.Pattern.Match(text);
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }

            if (!match.Success)
                continue;

            return template.Render(name =>
            {
                var value = match.Groups[name].Value;
                return template.Kinds[name] switch
                {
                    'w' => TranslateWord(StripPropertyPath(value)) ?? value,
                    't' => TranslateText(value.Trim(), depth + 1) ?? value,
                    _ => LocalizeValue(value),
                };
            });
        }

        return null;
    }

    /// <summary>"Lines[0].Quantity" → "Quantity" (FluentValidation property paths).</summary>
    private static string StripPropertyPath(string value)
    {
        var withoutIndex = CollectionIndex.Replace(value, string.Empty);
        var dot = withoutIndex.LastIndexOf('.');
        return dot >= 0 && dot < withoutIndex.Length - 1 ? withoutIndex[(dot + 1)..] : withoutIndex;
    }

    /// <summary>Dates written "13 Nov 2026" / "13-Nov-2026" get Bangla month names; everything else is kept.</summary>
    private static string LocalizeValue(string value)
    {
        if (value.Length < 8)
            return value;
        try
        {
            return EnglishDate.Replace(value, m =>
                string.Create(CultureInfo.InvariantCulture,
                    $"{m.Groups["day"].Value} {BanglaMonths[m.Groups["month"].Value]} {m.Groups["year"].Value}"));
        }
        catch (RegexMatchTimeoutException)
        {
            return value;
        }
    }

    private static string NormalizeWord(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var c in word)
        {
            if (c is not (' ' or '_' or '-'))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static List<Template> Compile(IEnumerable<KeyValuePair<string, string>> entries)
    {
        var templates = new List<Template>();
        var order = 0;
        foreach (var (english, bangla) in entries)
            templates.Add(Template.Create(english, bangla, order++));

        // Most specific first: a template with more fixed text wins over a generic one
        // ("Tag ID is required." before "{field:w} is required.").
        return templates
            .OrderByDescending(t => t.LiteralLength)
            .ThenBy(t => t.Order)
            .ToList();
    }

    private sealed class Template
    {
        private Template(Regex pattern, string bangla, IReadOnlyDictionary<string, char> kinds, int literalLength, int order)
        {
            Pattern = pattern;
            Bangla = bangla;
            Kinds = kinds;
            LiteralLength = literalLength;
            Order = order;
        }

        public Regex Pattern { get; }

        public string Bangla { get; }

        public IReadOnlyDictionary<string, char> Kinds { get; }

        public int LiteralLength { get; }

        public int Order { get; }

        public static Template Create(string english, string bangla, int order)
        {
            var pattern = new StringBuilder("^");
            var kinds = new Dictionary<string, char>(StringComparer.Ordinal);
            var literalLength = 0;
            var last = 0;
            foreach (Match placeholder in PlaceholderPattern.Matches(english))
            {
                var literal = english[last..placeholder.Index];
                literalLength += literal.Length;
                pattern.Append(Regex.Escape(literal));

                var name = placeholder.Groups["name"].Value;
                if (kinds.ContainsKey(name))
                {
                    pattern.Append(CultureInfo.InvariantCulture, $@"\k<{name}>");
                }
                else
                {
                    var kind = placeholder.Groups["kind"].Success ? placeholder.Groups["kind"].Value[0] : ' ';
                    kinds[name] = kind;

                    // A value never runs over into the next sentence ("… 1 kg. Insufficient stock …"),
                    // so joined messages split into their own sentences; only {x:t} takes the rest.
                    pattern.Append("(?<").Append(name).Append('>')
                        .Append(kind == 't' ? ".+?" : @"(?:(?![.!?]\s+['A-Z]).)+?")
                        .Append(')');
                }

                last = placeholder.Index + placeholder.Length;
            }

            var tail = english[last..];
            literalLength += tail.Length;
            pattern.Append(Regex.Escape(tail)).Append('$');

            var regex = new Regex(pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline, MatchTimeout);
            return new Template(regex, bangla, kinds, literalLength, order);
        }

        public string Render(Func<string, string> valueOf) =>
            PlaceholderPattern.Replace(Bangla, m =>
            {
                var name = m.Groups["name"].Value;
                return Kinds.ContainsKey(name) ? valueOf(name) : m.Value;
            });
    }
}
