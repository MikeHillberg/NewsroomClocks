using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TrayTime;

/// <summary>
/// Location in the cityMap.json file of a city
/// </summary>
public class CityInfoLocation
{
    static List<CityInfoLocation>? _cityIndices = null;

    public int Offset { get; set; }
    public int Length { get; set; }
    public string Name { get; set; }
    public CityInfoLocation(int offset, int length, string name)
    {
        Offset = offset;
        Length = length;
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    /// Use the CityMapItemLocation to get the CityMapItem from the assets file
    /// </summary>
    async public Task<CityInfo> GetCityInfoAsync()
    {
        //var uri = new Uri("ms-appx:///Assets/cityMap.json");
        //var file = StorageFile.GetFileFromApplicationUriAsync(uri).AsTask().Result;
        var file = await App.AssetProvider!.GetAssetAsync("cityMap.json");
        return GetCityInfoFromFile(this, file);
    }

    /// <summary>
    /// Read from the city index file at the index to get the CityInfo
    /// </summary>
    private static CityInfo GetCityInfoFromFile(CityInfoLocation cityIndex, StorageFile file)
    {
        using (Stream fs = file.OpenStreamForReadAsync().Result)
        {
            fs.Seek(cityIndex.Offset, SeekOrigin.Begin);

            // Read bytes, then convert to string
            byte[] buffer = new byte[cityIndex.Length];
            int totalRead = 0;
            while (totalRead < cityIndex.Length)
            {
                int bytesRead = fs.Read(buffer, totalRead, cityIndex.Length - totalRead);
                if (bytesRead == 0)
                    break;
                totalRead += bytesRead;
            }

            // Convert bytes to string
            string json = Encoding.UTF8.GetString(buffer, 0, totalRead);

            // Remove trailing comma (this is in a list and we only want one record)
            json = json.Replace("},", "}");

            // Deserialize string to CityInfo and return
            var cityInfo = System.Text.Json.JsonSerializer.Deserialize<CityInfo>(json);
            return cityInfo!;
        }
    }

    async public static Task<List<CityInfoLocation>> GetCityIndices()
    {
        if (_cityIndices == null)
        {
            _cityIndices = new();

            //var uri = new Uri("ms-appx:///Assets/city-index.txt");
            //var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            var file = await App.AssetProvider!.GetAssetAsync("CityMapIndex.txt");

            using (Stream fs = await file.OpenStreamForReadAsync())
            using (StreamReader reader = new StreamReader(fs))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    CityInfoLocation cityIndex = ParseCityMapIndexLine(line);
                    _cityIndices.Add(cityIndex);
                }
            }
        }
        return _cityIndices;
    }

    /// <summary>
    /// Parse a line from the city index into a CityIndex
    /// </summary>
    public static CityInfoLocation ParseCityMapIndexLine(string line)
    {
        var parts = line.Split(':');
        var cityIndex = new CityInfoLocation(
            int.Parse(parts[0]),
            int.Parse(parts[1]),
            parts[2]);
        return cityIndex;
    }


}
