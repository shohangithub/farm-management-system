using System.Security.Cryptography;
using System.Text;

namespace Farm360.Api.Configuration;

/// <summary>
/// Logs CRITICAL warnings (never crashes) when a non-Development environment runs with
/// development-only security settings.
/// </summary>
public static class StartupSecurityChecks
{
    /// <summary>
    /// SHA-256 of the JWT signing key committed in appsettings.Development.json, so the check works
    /// even when that file is not deployed. Only the hash is kept here, never the key.
    /// </summary>
    internal const string DevelopmentJwtKeySha256 = "c98a9fe1b82be6074d1fda62bc66ba6b85896d0a993a587c234e8bb6e0fa0957";

    public static IReadOnlyList<string> Evaluate(IConfiguration configuration, IHostEnvironment environment)
    {
        var problems = new List<string>();
        if (environment.IsDevelopment())
        {
            return problems;
        }

        var jwtSecret = configuration["Jwt:Secret"];
        if (!string.IsNullOrEmpty(jwtSecret) && IsDevelopmentJwtKey(jwtSecret, environment))
        {
            problems.Add("JWT signing key (Jwt:Secret) is the development key from appsettings.Development.json. " +
                         "Anyone with the repository can forge tokens. Set a new secret via environment/secret store.");
        }

        if (configuration.GetValue("SslCommerz:IsSandbox", defaultValue: true))
        {
            problems.Add("SslCommerz:IsSandbox is true outside Development: real payments will not be verified " +
                         "against the live gateway.");
        }

        return problems;
    }

    public static void LogFindings(IConfiguration configuration, IHostEnvironment environment, Microsoft.Extensions.Logging.ILogger logger)
    {
        foreach (var problem in Evaluate(configuration, environment))
        {
            logger.LogCritical("SECURITY MISCONFIGURATION ({Environment}): {Problem}", environment.EnvironmentName, problem);
        }
    }

    internal static bool IsDevelopmentJwtKey(string secret, IHostEnvironment environment)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
        if (hash == DevelopmentJwtKeySha256)
        {
            return true;
        }

        // Also compare against the Development settings file if it ships alongside the app
        // (covers a future change of the dev key without updating the hash above).
        try
        {
            var devFile = Path.Combine(environment.ContentRootPath, "appsettings.Development.json");
            if (File.Exists(devFile))
            {
                var devConfig = new ConfigurationBuilder().AddJsonFile(devFile, optional: true).Build();
                var devSecret = devConfig["Jwt:Secret"];
                return !string.IsNullOrEmpty(devSecret) && string.Equals(devSecret, secret, StringComparison.Ordinal);
            }
        }
        catch (Exception)
        {
            // Best effort only -- never block startup on this check.
        }

        return false;
    }
}
