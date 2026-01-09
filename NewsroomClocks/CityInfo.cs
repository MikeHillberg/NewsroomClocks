using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace NewsroomClocks;

/// <summary>
/// City information from cityMap.json
/// </summary>
public class CityInfo
{
    private static Dictionary<string, string>? _ianaToWindowsMap;
    private static Dictionary<string, string>? _zoneAliases;
    private TimeZoneInfo? _timeZoneInfo = null;

    // This is from ZoneParser.java in CLDR, and maps new names to old names,
    // e.g. Katmandu to Kathmandu (with an "h").
    // The CLDR windowsZones.xml use the old names
    private static Dictionary<string, string> FIX_UNSTABLE_TZID_DATA = new()
    {
        {"America/Atikokan", "America/Coral_Harbour"},
        {"America/Argentina/Buenos_Aires", "America/Buenos_Aires"},
        {"America/Argentina/Catamarca", "America/Catamarca"},
        {"America/Argentina/Cordoba", "America/Cordoba"},
        {"America/Argentina/Jujuy", "America/Jujuy"},
        {"America/Argentina/Mendoza", "America/Mendoza"},
        {"America/Nuuk", "America/Godthab"},
        {"America/Kentucky/Louisville", "America/Louisville"},
        {"America/Indiana/Indianapolis", "America/Indianapolis"},
        {"Africa/Asmara", "Africa/Asmera"},
        {"Atlantic/Faroe", "Atlantic/Faeroe"},
        {"Asia/Kolkata", "Asia/Calcutta"},
        {"Asia/Ho_Chi_Minh", "Asia/Saigon"},
        {"Asia/Yangon", "Asia/Rangoon"},
        {"Asia/Kathmandu", "Asia/Katmandu"},
        {"Europe/Kyiv", "Europe/Kiev"},
        {"Pacific/Pohnpei", "Pacific/Ponape"},
        {"Pacific/Chuuk", "Pacific/Truk"},

        // This is the one exception where cityMap.json uses the new name
        //{"Pacific/Honolulu", "Pacific/Johnston"},

        {"Pacific/Kanton", "Pacific/Enderbury"}
    };


    // Constructor for Json deserializer
    [JsonConstructor]
    public CityInfo() { }

    internal static async Task<CityInfo> CreateFromJsonAsync(string json)
    {
        var cityInfo = JsonSerializer.Deserialize(json, CityInfoJsonContext.Default.CityInfo)!;

        await cityInfo.LoadZoneAliasesAsync();
        await cityInfo.LoadTimeZoneInfoAsync();

        return cityInfo;
    }

    /// <summary>
    /// Load the zoneAlias info from cldr\common\supplemental\supplementalMetadata.xml
    /// </summary>
    async Task LoadZoneAliasesAsync()
    {
        if (_zoneAliases != null)
        {
            return;
        }

        // Load zoneAlias.txt, which has info copied from 
        // "cldr\common\supplemental\supplementalMetadata.xml",
        // the zoneAlias info that maps old to new names. E.g. this in supplementalMetadata.xml:
        //
        // <zoneAlias type="Pacific/Johnston" replacement="Pacific/Honolulu" reason="deprecated"/>
        //
        // becomes this in zoneAlias.txt
        //
        // "Pacific/Johnston" : "Pacific/Honolulu"

        var storageFile = await App.AssetProvider!.GetAssetAsync("zoneAliases.txt");
        var stream = await storageFile.OpenStreamForReadAsync();
        var textReader = new StreamReader(stream);

        _zoneAliases = new Dictionary<string, string>();
        string? line;
        while ((line = await textReader.ReadLineAsync()) != null)
        {
            var parts = line.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                // Remove quotes from the strings (bugbug)
                var key = parts[0].Trim('"');
                var value = parts[1].Trim('"');
                _zoneAliases[key] = value;
            }
        }
    }


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
            // Check if this is one of the corrections from CLDR's ZoneParser.java
            if (FIX_UNSTABLE_TZID_DATA.TryGetValue(OriginalTimezone, out var corrected))
            {
                return corrected;
            }

            // Check if this is one of the replacements from CLDR's supplementalMetadata.xml
            // Note that this actually produces an older name, but it's what windowsZones.xml uses
            if (_zoneAliases!.TryGetValue(OriginalTimezone, out corrected))
            {
                return corrected;
            }

            // Otherwise use the Iana time zone code as given in cityMap.json
            return OriginalTimezone;
        }
    }


    public override string ToString()
    {
        return $"{City}, {Province}, {Iso3}";
    }

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
                            _ianaToWindowsMap[ianaId] = windowsId;
                        }
                    }
                }
            }
        }
        return _ianaToWindowsMap;
    }

}

/// <summary>
/// JSON serialization context for CityInfo to support AoT compilation
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(CityInfo))]
internal partial class CityInfoJsonContext : JsonSerializerContext
{
}