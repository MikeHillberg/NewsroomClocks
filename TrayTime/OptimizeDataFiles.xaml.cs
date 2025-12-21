using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace TrayTime;

/// <summary>
/// (Debug only) Tools to optimize CityMap and WindowsZones data files
/// </summary>
public sealed partial class OptimizeDataFiles : UserControl
{
    public OptimizeDataFiles()
    {
        InitializeComponent();
    }

    private async void CreateCityMapIndex(object sender, RoutedEventArgs e)
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

    private async void CompileWindowsZones(object sender, RoutedEventArgs e)
    {
        // Get TextReader for windowsZones.xml
        var uri = new Uri("ms-appx:///Assets/windowsZones.xml");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        var stream = await file.OpenStreamForReadAsync();
        var textReader = new StreamReader(stream);

        // Create XmlReader with settings to ignore DTD
        var settings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Ignore
        };
        using var xmlReader = System.Xml.XmlReader.Create(textReader, settings);
        Dictionary<string, string> map = new();


        // Example:
        // <mapZone other = "SA Western Standard Time" territory = "001" type = "America/La_Paz" />
        // <mapZone other = "SA Western Standard Time" territory = "AG" type = "America/Antigua" />
        // <mapZone other = "SA Western Standard Time" territory = "AI" type = "America/Anguilla" />
        // <mapZone other = "SA Western Standard Time" territory = "AW" type = "America/Aruba" />

        // Loop through all mapZone elements
        while (xmlReader.Read())
        {
            if (xmlReader.NodeType == System.Xml.XmlNodeType.Element &&
                xmlReader.Name == "mapZone")
            {
                string other = xmlReader.GetAttribute("other") ?? string.Empty;
                string territory = xmlReader.GetAttribute("territory") ?? string.Empty;
                string type = xmlReader.GetAttribute("type") ?? string.Empty;

                map.TryAdd(type, other);
            }
        }

        StringBuilder sb = new();
        foreach (var kvp in map)
        {
            sb.AppendLine($"{kvp.Key} : {kvp.Value}");
        }

        // TODO: Use WinUI clipboard instead
        // System.Windows.Forms.Clipboard.SetText(sb.ToString());
    }


    static string GetSecondFromColonSeparatedValue(string line)
    {
        return line.Split(':')[1].Trim().Trim(',', '"');
    }

}
