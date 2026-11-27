namespace Farm360.Application.Common.Localization;

/// <summary>
/// Translates user-facing server messages (exception text, validation errors, ProblemDetails titles)
/// into the language the client asked for. Messages are matched against templates of the English
/// text; anything without a template is returned unchanged, so an untranslated message still reads
/// correctly in English.
/// </summary>
public interface IMessageTranslator
{
    /// <summary>
    /// The message in <paramref name="language"/> ("en" or "bn"), or <paramref name="message"/>
    /// unchanged when the language is English or no template matches.
    /// </summary>
    string Translate(string message, string language);
}
