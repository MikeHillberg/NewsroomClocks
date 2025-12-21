using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace TrayTime;

/// <summary>
/// City information from cityMap.json
/// </summary>
public class CityInfo
{
    // Private constructor except to Json deserializer
    [JsonConstructor]
    private CityInfo() { } 

    internal static async Task<CityInfo> CreateFromJsonAsync(string json)
    {
        var cityInfo = System.Text.Json.JsonSerializer.Deserialize<CityInfo>(json)!;
        await cityInfo.LoadTimeZoneInfoAsync();
        return cityInfo;
    }

    // Map Iana timezone to Windows timezone
    private static Dictionary<string, string>? _ianaToWindowsMap;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("city_ascii")]
    public string CityAscii { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lng")]
    public double Longitude { get; set; }

    // For some reason population can be a half
    [JsonPropertyName("pop")]
    public double Population { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    // For some reason iso2 can be an int (e.g. Pec, Ðakovica in Kosovo)
    [JsonPropertyName("iso2")]
    public object Iso2 { get; set; } = string.Empty;

    [JsonPropertyName("iso3")]
    public string Iso3 { get; set; } = string.Empty;

    [JsonPropertyName("province")]
    public string Province { get; set; } = string.Empty;

    // Map Json timezone property to OriginalTimezone property,
    // and then the IanaTimezone property is a corrected version of the original
    [JsonPropertyName("timezone")]
    public string OriginalTimezone { get; set; } = string.Empty;

    /// <summary>
    /// Correct Iana timezone, corrected from OriginalTimezone property
    /// </summary>
    public string IanaTimezone
    {
        get
        {
            if (_corrections.TryGetValue(OriginalTimezone, out var corrected))
            {
                return corrected;
            }
            return OriginalTimezone;
        }
    }



    // city-map provides an Iana timezone, but not all of those time zones are in the CLDR
    // (windowsZones.xml). The Iana timezones dynamic and change, and I think
    // city-map is out of date in some cases. In some cases city-map seems to be
    // correct but CLDR is missing the zone.
    // So below are conversions from a city-map Iana timezone to a correct or
    // compatible one in CLDR, which I'm 99% sure is correct (bugbug)
    Dictionary<string, string> _corrections = new()
    {
        {"America/Argentina/Catamarca", "America/Argentina/Salta" },
        {"America/Argentina/Mendoza", "America/Argentina/Salta" },
        {"America/Argentina/Buenos_Aires", "America/Buenos_Aires" },
        {"America/Argentina/Cordoba", "America/Cordoba" },
        {"America/Argentina/Jujuy", "America/Jujuy" },
        {"America/Yellowknife", "America/Edmonton" },
        { "Africa/Asmara", "Africa/Nairobi" },
        { "America/Indiana/Indianapolis", "America/Indianapolis" },
        { "America/Montreal", "America/Toronto" },
        { "America/Nipigon", "America/Toronto" },
        { "America/Pangnirtung", "America/Iqaluit" },
        { "America/Thunder_Bay", "America/Toronto" },
        { "Asia/Chongqing", "Asia/Shanghai" },
        { "Asia/Harbin", "Asia/Shanghai" },
        { "Asia/Kashgar", "Asia/Urumqi" },

        { "America/Atikokan", "America/Panama" },

        // Mongolia
        { "Asia/Choibalsan", "Asia/Ulaanbaatar" },

        //// Nepal
        //{ "Asia/Kathmandu", "Asia/Kathmandu" }, // canonical, maps directly to Nepal Standard Time

        // Vietnam
        { "Asia/Ho_Chi_Minh", "Asia/Bangkok" }, // For UTC+7 (no DST), CLDR uses Asia/Bangkok as the representative IANA zone.


        // US (Louisville)
        { "America/Kentucky/Louisville", "America/Louisville" },

        // Faroe Islands
        { "Atlantic/Faroe", "Atlantic/Faeroe" }, // CLDR spelling

        // Ukraine
        { "Europe/Kyiv", "Europe/Kiev" },        // CLDR still uses Kiev spelling
        { "Europe/Uzhgorod", "Europe/Kiev" },
        { "Europe/Zaporozhye", "Europe/Kiev" },

        // Micronesia
        { "Pacific/Pohnpei", "Pacific/Ponape" }  // CLDR spelling
    };

    public override string ToString()
    {
        return $"{City}, {Province}, {Iso3}";
    }

    TimeZoneInfo? _timeZoneInfo = null;

    /// <summary>
    /// Gets the TimeZoneInfo for binding purposes. Call LoadTimeZoneInfoAsync first.
    /// </summary>
    public TimeZoneInfo? TimeZoneInfo => _timeZoneInfo;

    /// <summary>
    /// Loads the TimeZoneInfo asynchronously from app assets.
    /// </summary>
    private async Task LoadTimeZoneInfoAsync()
    {
        if (_timeZoneInfo == null)
        {
            var map = await GetIanaToWindowsMapAsync();
            if (map.TryGetValue(IanaTimezone, out var windowsTimeZoneID))
            {
                _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsTimeZoneID);
            }
        }
    }

    private static async System.Threading.Tasks.Task<Dictionary<string, string>> GetIanaToWindowsMapAsync()
    {
        if (_ianaToWindowsMap == null)
        {
            _ianaToWindowsMap = new Dictionary<string, string>();

            //var uri = new Uri("ms-appx:///Assets/Iana2WindowsTimeZoneID.txt");
            //var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var file = await App.AssetProvider!.GetAssetAsync("Iana2WindowsTimeZoneID.txt");

            var lines = await FileIO.ReadLinesAsync(file);

            foreach (var line in lines)
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    var ianaIds = parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var windowsId = parts[1];

                    // Map each IANA ID to the Windows ID
                    foreach (var ianaId in ianaIds)
                    {
                        if (!_ianaToWindowsMap.ContainsKey(ianaId))
                        {
                            if (ianaId == "Asia/Kathmandu")
                            {
                                // Asia/Kathmandu is a valid Iana time, but not in windowsZones.xml
                                // and I can't find anything equivalent, so handling it specially here
                                // in the Iana->Windows mapping
                                _ianaToWindowsMap[ianaId] = "Nepal Standard Time";
                            }
                            else
                            {
                                _ianaToWindowsMap[ianaId] = windowsId;
                            }
                        }
                    }
                }
            }
        }
        return _ianaToWindowsMap;
    }


}