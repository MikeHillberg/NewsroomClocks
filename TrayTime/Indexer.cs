using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TrayTime;

internal class Indexer
{

    //{
    //    "city": "Damascus",
    //    "city_ascii": "Damascus",
    //    "lat": 33.500034,
    //    "lng": 36.29999589,
    //    "pop": 2466000,
    //    "country": "Syria",
    //    "iso2": "SY",
    //    "iso3": "SYR",
    //    "province": "Damascus",
    //    "timezone": "Asia/Damascus"
    //},



    async static internal void ProcessFile()
    {
        // Get cityMap.json from resources as a FileStream
        var uri = new Uri("ms-appx:///Assets/cityMap.json");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        if (file == null) return;

        try
        {
            List<(int, int, string)> index = new();

            using (Stream fs = await file.OpenStreamForReadAsync())
            using (StreamReader reader = new StreamReader(fs, Encoding.UTF8))
            {
                int offset = 0;
                string? line = null;
                List<int> offsets = new();
                int firstOffset = 0;
                StringBuilder sb = new();

                string city = "";
                string province = "";
                string country = "";

                // Calculate byte length of newline for UTF8
                int newlineByteLength = Encoding.UTF8.GetByteCount(Environment.NewLine);

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim() == "{")
                    {
                        firstOffset = offset;
                        sb = new();
                    }
                    else if (line.Contains("city_ascii"))
                    {
                        city = GetValue(line);
                    }
                    else if (line.Contains("province"))
                    {
                        province = GetValue(line);
                    }
                    else if (line.Contains("iso3"))
                    {
                        country = GetValue(line);
                    }

                    // Calculate byte offset instead of character offset
                    offset += Encoding.UTF8.GetByteCount(line) + newlineByteLength;

                    if (line.Trim().StartsWith("}"))
                    {
                        index.Add((
                            firstOffset,
                            offset - firstOffset,
                            $"{city}, {province}, {country}"
                            ));
                    }
                }

                StringBuilder sb2 = new();
                foreach (var i in index)
                {
                    sb2.AppendLine(@$"{i.Item1}:{i.Item2}:{i.Item3}");
                }
                // TODO: Use WinUI clipboard instead
                // Clipboard.SetText(sb2.ToString());
                Debug.WriteLine("City index (WinForms Clipboard removed):");
                Debug.WriteLine(sb2.ToString());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading file: {ex.Message}");
        }
    }

    static string GetValue(string line)
    {
        return line.Split(':')[1].Trim().Trim(',', '"');
    }

    static List<CityIndex>? _cities = null;

    async public static Task<List<CityIndex>> GetCityIndices()
    {
        if (_cities == null)
        {
            _cities = new();

            var uri = new Uri("ms-appx:///Assets/city-index.txt");
            var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            using (Stream fs = await file.OpenStreamForReadAsync())
            using (StreamReader reader = new StreamReader(fs))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(':');
                    _cities.Add(new CityIndex(
                        int.Parse(parts[0]), 
                        int.Parse(parts[1]), 
                        parts[2]));
                }
            }
        }
        return _cities;
    }

    static internal CityDetails GetCityDetails(CityIndex cityIndex)
    {
        var uri = new Uri("ms-appx:///Assets/cityMap.json");
        var file =  StorageFile.GetFileFromApplicationUriAsync(uri).AsTask().Result;
        using (Stream fs =  file.OpenStreamForReadAsync().Result)
        {
            fs.Seek(cityIndex.Offset, SeekOrigin.Begin);
            using (StreamReader reader = new StreamReader(fs))
            {
                char[] buffer = new char[cityIndex.Length];
                reader.ReadBlock(buffer, 0, cityIndex.Length);

                string json = new string(buffer);
                json = json.Replace("},", "}"); // Remove trailing comma if present

                var cityDetails = System.Text.Json.JsonSerializer.Deserialize<CityDetails>(json);
                return cityDetails!;
            }
        }
    }

}

internal class CityIndex
{
    public int Offset { get; set; }
    public int Length { get; set; }
    public string Name { get; set; }
    public CityIndex(int offset, int length, string name)
    {
        Offset = offset;
        Length = length;
        Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}