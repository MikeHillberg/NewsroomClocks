using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace TrayTime;

public class Indexer
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


    [Conditional("DEBUG")]
    async static internal void CreateIndexOfCityMap()
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

                // Record variables
                string city = "";
                string province = "";
                string country = "";
                string timezone = "";

                // Reinit the above variables
                Action reinitRecordVariables = () =>
                {
                    city = "";
                    province = "";
                    country = "";
                    timezone = "";
                };

                // Calculate byte length of newline for UTF8
                int newlineByteLength = Encoding.UTF8.GetByteCount(Environment.NewLine);

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim() == "{")
                    {
                        firstOffset = offset;
                        sb = new();
                    }
                    // Be careful to distinguish city from city_ascii
                    else if (line.Contains(@"""city"""))
                    {
                        city = GetSecondFromColonSeparatedValue(line);
                    }
                    else if (line.Contains("province"))
                    {
                        province = GetSecondFromColonSeparatedValue(line);
                    }
                    else if (line.Contains("iso3"))
                    {
                        country = GetSecondFromColonSeparatedValue(line);
                    }
                    else if (line.Contains("timezone"))
                    {
                        timezone = GetSecondFromColonSeparatedValue(line);
                    }

                    // Update offset to be the next record
                    offset += Encoding.UTF8.GetByteCount(line) + newlineByteLength;

                    if (line.Trim().StartsWith("}"))
                    {
                        // At the end of a record, so add to the index.
                        // Skip though if it doesn't have a time zone (Antartica doesn't have a time zone)
                        if (!timezone.Contains("null"))
                        {
                            index.Add((
                                firstOffset,
                                offset - firstOffset,
                                $"{city}, {province}, {country}"
                                ));
                        }

                        // Clear city/province/counter for the next pass
                        reinitRecordVariables();
                    }
                }

                StringBuilder sb2 = new();
                foreach (var i in index)
                {
                    sb2.AppendLine(@$"{i.Item1}:{i.Item2}:{i.Item3}");
                }

                // bugbug: this isn't working
                DataPackage dp = new();
                dp.SetText(sb2.ToString());
                Clipboard.SetContent(dp);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error reading file: {ex.Message}");
        }
    }

    static string GetSecondFromColonSeparatedValue(string line)
    {
        return line.Split(':')[1].Trim().Trim(',', '"');
    }

}
