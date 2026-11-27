using System.Globalization;
using Farm360.Api.Localization;
using Farm360.Application.Common.Localization;
using FluentValidation;
using Microsoft.AspNetCore.Localization;

namespace Farm360.Api.Configuration;

/// <summary>
/// Request language for user-facing messages. The web app sends <c>Accept-Language: bn</c> or
/// <c>en</c>; only the UI culture follows it. <see cref="CultureInfo.CurrentCulture"/> (number
/// and date formatting) stays the process culture for every request, so API JSON (always
/// invariant in System.Text.Json anyway), report rendering and sorting are unchanged; reports
/// keep their own language parameter. Error messages are translated where they become
/// ProblemDetails (<see cref="ProblemDetailsLocalizer"/>).
/// </summary>
public static class LocalizationSetup
{
    public static IServiceCollection AddFarm360Localization(this IServiceCollection services)
    {
        services.AddSingleton<IMessageTranslator>(MessageTranslator.Default);

        // FluentValidation would otherwise follow CurrentUICulture and emit its own built-in
        // Bengali text; keep its defaults English so the catalogue (with Bangla field names) is the
        // single source of Bangla.
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");

        // Results.Problem(...) in endpoints goes through IProblemDetailsService.
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                ProblemDetailsLocalizer.Localize(context.ProblemDetails, context.HttpContext));

        return services;
    }

    public static IApplicationBuilder UseFarm360RequestLocalization(this IApplicationBuilder app)
    {
        // Captured once: every request keeps exactly this formatting culture.
        var formattingCulture = CultureInfo.CurrentCulture;
        var uiCultures = new List<CultureInfo> { CultureInfo.GetCultureInfo("en") };
        try
        {
            uiCultures.Add(CultureInfo.GetCultureInfo(MessageTranslator.Bangla));
        }
        catch (CultureNotFoundException)
        {
            // Globalization-invariant host: Bangla cannot be selected; messages stay English.
        }

        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(formattingCulture, uiCultures[0]),
            SupportedCultures = [formattingCulture],
            SupportedUICultures = uiCultures,
            FallBackToParentUICultures = true,
            ApplyCurrentCultureToResponseHeaders = false,
        };
        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

        return app.UseRequestLocalization(options);
    }
}
