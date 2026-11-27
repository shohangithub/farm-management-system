namespace Farm360.Application.Weather;

/// <summary>A WMO weather code as farmers read it: plain English, Bangla and a Material icon name.</summary>
public sealed record WeatherCondition(int Code, string English, string Bangla, string Icon);

/// <summary>
/// WMO weather interpretation codes (WW), as returned by Open-Meteo, mapped to friendly text.
/// Icons are classic Material Icons ligatures (the web loads the "Material Icons" font).
/// </summary>
public static class WeatherCodes
{
    private static readonly Dictionary<int, WeatherCondition> Map = new()
    {
        [0] = new(0, "Clear sky", "পরিষ্কার আকাশ", "wb_sunny"),
        [1] = new(1, "Mostly clear", "প্রায় পরিষ্কার আকাশ", "wb_sunny"),
        [2] = new(2, "Partly cloudy", "আংশিক মেঘলা", "wb_cloudy"),
        [3] = new(3, "Cloudy", "মেঘলা", "cloud"),
        [45] = new(45, "Fog", "কুয়াশা", "blur_on"),
        [48] = new(48, "Freezing fog", "ঘন কুয়াশা", "blur_on"),
        [51] = new(51, "Light drizzle", "হালকা গুঁড়ি গুঁড়ি বৃষ্টি", "grain"),
        [53] = new(53, "Drizzle", "গুঁড়ি গুঁড়ি বৃষ্টি", "grain"),
        [55] = new(55, "Heavy drizzle", "ঘন গুঁড়ি গুঁড়ি বৃষ্টি", "grain"),
        [56] = new(56, "Freezing drizzle", "ঠান্ডা গুঁড়ি গুঁড়ি বৃষ্টি", "grain"),
        [57] = new(57, "Freezing drizzle", "ঠান্ডা গুঁড়ি গুঁড়ি বৃষ্টি", "grain"),
        [61] = new(61, "Light rain", "হালকা বৃষ্টি", "umbrella"),
        [63] = new(63, "Rain", "বৃষ্টি", "umbrella"),
        [65] = new(65, "Heavy rain", "ভারী বৃষ্টি", "umbrella"),
        [66] = new(66, "Freezing rain", "ঠান্ডা বৃষ্টি", "umbrella"),
        [67] = new(67, "Freezing rain", "ঠান্ডা বৃষ্টি", "umbrella"),
        [71] = new(71, "Light snow", "হালকা তুষারপাত", "ac_unit"),
        [73] = new(73, "Snow", "তুষারপাত", "ac_unit"),
        [75] = new(75, "Heavy snow", "ভারী তুষারপাত", "ac_unit"),
        [77] = new(77, "Snow grains", "তুষারকণা", "ac_unit"),
        [80] = new(80, "Light showers", "হালকা ঝরো বৃষ্টি", "umbrella"),
        [81] = new(81, "Showers", "ঝরো বৃষ্টি", "umbrella"),
        [82] = new(82, "Heavy showers", "প্রবল ঝরো বৃষ্টি", "umbrella"),
        [85] = new(85, "Snow showers", "তুষার ঝরা", "ac_unit"),
        [86] = new(86, "Heavy snow showers", "প্রবল তুষার ঝরা", "ac_unit"),
        [95] = new(95, "Thunderstorm", "বজ্রঝড়", "flash_on"),
        [96] = new(96, "Thunderstorm with hail", "শিলাসহ বজ্রঝড়", "flash_on"),
        [99] = new(99, "Thunderstorm with heavy hail", "প্রবল শিলাসহ বজ্রঝড়", "flash_on"),
    };

    /// <summary>Unknown codes read as "Unknown weather" with a neutral icon, never an error.</summary>
    public static WeatherCondition Describe(int code) =>
        Map.TryGetValue(code, out var condition) ? condition : new WeatherCondition(code, "Unknown weather", "আবহাওয়া জানা নেই", "help_outline");

    /// <summary>WMO 95, 96, 99: thunderstorm (with or without hail).</summary>
    public static bool IsThunderstorm(int code) => code is 95 or 96 or 99;
}
