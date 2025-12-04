using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Windows.Storage;

namespace TrayTime;

internal class CityDetails
{
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

    [JsonPropertyName("iso2")]
    public string Iso2 { get; set; } = string.Empty;

    [JsonPropertyName("iso3")]
    public string Iso3 { get; set; } = string.Empty;

    [JsonPropertyName("province")]
    public string Province { get; set; } = string.Empty;

    [JsonPropertyName("timezone")]
    public string Timezone { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{City}, {Province}, {Country}";
    }

    TimeZoneInfo? _timeZoneInfo = null;
    
    /// <summary>
    /// Gets the TimeZoneInfo for binding purposes. Call LoadTimeZoneInfoAsync first.
    /// </summary>
    public TimeZoneInfo? TimeZoneInfo => _timeZoneInfo;
    
    /// <summary>
    /// Loads the TimeZoneInfo asynchronously from app assets.
    /// </summary>
    public async System.Threading.Tasks.Task LoadTimeZoneInfoAsync()
    {
        if (_timeZoneInfo == null)
        {
            var map = await GetIanaToWindowsMapAsync();
            if (map.TryGetValue(Timezone, out var windowsTimeZoneID))
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
            var uri = new Uri("ms-appx:///Assets/Iana2WindowsTimeZoneID.txt");
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
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