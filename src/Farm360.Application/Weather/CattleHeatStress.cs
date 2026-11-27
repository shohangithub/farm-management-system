namespace Farm360.Application.Weather;

/// <summary>Heat-stress band for beef cattle, from the Temperature-Humidity Index.</summary>
public enum HeatStressBand
{
    Comfortable = 0,
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Danger = 4,
}

/// <summary>
/// Temperature-Humidity Index (THI) and the beef-cattle heat-stress band. Pure maths, no I/O.
/// </summary>
public static class CattleHeatStress
{
    /// <summary>
    /// THI = (1.8·T + 32) − (0.55 − 0.0055·RH) · (1.8·T − 26), with T in °C and RH in %.
    /// <para>
    /// This is the NRC (1971) formula (the one most beef and dairy heat-stress tables use). It is the
    /// dry-bulb temperature in °F minus a humidity correction: at 100% RH the correction is zero and the
    /// THI equals the temperature in °F; drier air lowers it. Humidity is clamped to 0–100.
    /// </para>
    /// </summary>
    public static double Thi(double temperatureC, double relativeHumidityPercent)
    {
        var rh = Math.Clamp(relativeHumidityPercent, 0, 100);
        var tempF = (1.8 * temperatureC) + 32;
        return tempF - ((0.55 - (0.0055 * rh)) * ((1.8 * temperatureC) - 26));
    }

    /// <summary>The THI as shown to people: a whole number (half away from zero).</summary>
    public static int Rounded(double thi) => (int)Math.Round(thi, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Band on the rounded THI: below 72 comfortable, 72–78 mild, 79–88 moderate, 89–98 severe,
    /// above 98 danger (see <see cref="WeatherThresholds"/>).
    /// </summary>
    public static HeatStressBand Band(double thi)
    {
        var value = Rounded(thi);
        return value switch
        {
            >= WeatherThresholds.ThiDangerFrom => HeatStressBand.Danger,
            >= WeatherThresholds.ThiSevereFrom => HeatStressBand.Severe,
            >= WeatherThresholds.ThiModerateFrom => HeatStressBand.Moderate,
            >= WeatherThresholds.ThiMildFrom => HeatStressBand.Mild,
            _ => HeatStressBand.Comfortable,
        };
    }

    public static HeatStressBand Band(double temperatureC, double relativeHumidityPercent) =>
        Band(Thi(temperatureC, relativeHumidityPercent));
}
