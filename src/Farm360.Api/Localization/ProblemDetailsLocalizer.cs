using System.Globalization;
using Farm360.Application.Common.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Localization;

/// <summary>
/// The one place server messages are translated: every ProblemDetails written by the API
/// (exception handler, middleware, <c>Results.Problem</c>) passes through <see cref="Localize"/>.
/// Translates the title, the detail and each validation message; the validation dictionary keys
/// (property names) are left alone because clients bind them to form fields.
/// </summary>
public static class ProblemDetailsLocalizer
{
    /// <summary>The request's message language ("bn" or "en").</summary>
    public static string RequestLanguage(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var uiCulture = context.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture
            ?? CultureInfo.CurrentUICulture;
        return uiCulture.TwoLetterISOLanguageName;
    }

    /// <summary>Translates <paramref name="message"/> into the request's language.</summary>
    public static string Text(HttpContext context, string message)
    {
        var language = RequestLanguage(context);
        return MessageTranslator.IsBangla(language) ? Translator(context).Translate(message, language) : message;
    }

    /// <summary>Translates the title, detail and validation errors of <paramref name="problem"/> in place.</summary>
    public static void Localize(ProblemDetails problem, HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var language = RequestLanguage(context);
        if (!MessageTranslator.IsBangla(language))
            return;

        var translator = Translator(context);
        if (problem.Title is not null)
            problem.Title = translator.Translate(problem.Title, language);
        if (problem.Detail is not null)
            problem.Detail = translator.Translate(problem.Detail, language);

        if (problem.Extensions.TryGetValue("errors", out var errors) && errors is IDictionary<string, string[]> byField)
        {
            problem.Extensions["errors"] = byField.ToDictionary(
                e => e.Key,
                e => e.Value.Select(m => translator.Translate(m, language)).ToArray(),
                StringComparer.Ordinal);
        }
    }

    private static IMessageTranslator Translator(HttpContext context) =>
        context.RequestServices?.GetService<IMessageTranslator>() ?? MessageTranslator.Default;
}
