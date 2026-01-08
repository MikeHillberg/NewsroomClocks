using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NewsroomClocks;

/// <summary>
/// Helper class to generate data files, like indexing CityData.json.
/// Debug-only, not used at runtime
/// </summary>
static internal class GenerateDataFiles
{
    [Conditional("DEBUG")]
    /// <summary>
    /// Helper to generate data files, like indexing CityData.json.
    /// Debug-only, not used at runtime.
    /// Run in debugger; reads/writes files in repo
    /// </summary>
    static internal void Generate()
    {
        CreateCityMapIndex();
        CompileSupplementalData();
        CompileWindowsZones();
    }

    /// <summary>
    /// Read CityMap.json and produce CityMapIndex.txt
    /// </summary>
    [Conditional("DEBUG")]
    static void CreateCityMapIndex()
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

        var cityMapJsonPath =
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!,
                         @"..\..\..\..\..\Submodules\city-timezones\data\cityMap.json");


        try
        {
            List<(int, int, string)> index = new();

            using (Stream fs = File.OpenRead(cityMapJsonPath))
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

                var outputPath = Path.Combine(
                    Path.GetDirectoryName(Environment.ProcessPath!)!,
                    @"..\..\..\..\..\Assets\cityMapIndex.txt");

                var writer = new StreamWriter(File.OpenWrite(outputPath));
                writer.Write(sb2.ToString());
                writer.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in {nameof(CreateCityMapIndex)}: {ex.Message}");
        }
    }


    /// <summary>
    /// Pull zoneAlias elements out of CLDR supplementMetadata.xml and put
    /// key info into zoneAliases.txt
    /// </summary>
    [Conditional("DEBUG")]
    static void CompileSupplementalData()
    {
        var supplementalMetadataPath =
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!,
                         @"..\..\..\..\..\Submodules\cldr\common\supplemental\supplementalMetadata.xml");

        try
        {
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Ignore
            };
            using var xmlReader = System.Xml.XmlReader.Create(supplementalMetadataPath, settings);

            StringBuilder sb = new();

            // Loop through all elements, looking for zoneAlias elements
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == System.Xml.XmlNodeType.Element &&
                    xmlReader.Name == "zoneAlias")
                {
                    string type = xmlReader.GetAttribute("type") ?? string.Empty;
                    string replacement = xmlReader.GetAttribute("replacement") ?? string.Empty;

                    if (!string.IsNullOrEmpty(type) && !string.IsNullOrEmpty(replacement))
                    {
                        sb.AppendLine($"\"{type}\" : \"{replacement}\"");
                    }
                }
            }

            var outputPath = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath!)!,
                @"..\..\..\..\..\Assets\zoneAliases.txt");

            var writer = new StreamWriter(File.OpenWrite(outputPath));
            writer.Write(sb.ToString());
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in {nameof(CompileSupplementalData)}: {ex.Message}");
        }
    }

    /// <summary>
    /// Read windowsZones.xml from CLDR and produce Iana2WindowsTimeZoneID.txt
    /// </summary>
    [Conditional("DEBUG")]
    static void CompileWindowsZones()
    {
        try
        {
            // This is only meant to run in development, so ready it out of the repo
            var windowsZonesPath =
                Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!,
                             @"..\..\..\..\..\Submodules\cldr\common\supplemental\windowsZones.xml");
            var file = File.OpenRead(windowsZonesPath);
            var textReader = new StreamReader(file);

            // Create XmlReader with settings to ignore DTD
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Ignore
            };
            using var xmlReader = System.Xml.XmlReader.Create(textReader, settings);
            Dictionary<string, string> map = new();

            // Example:
            // <mapZone other = "SA Western Standard Time" territory = "001" type = "America/La_Paz" />
            // <mapZone other = "US Eastern Standard Time" territory = "US" type = "America/Indianapolis America/Indiana/Marengo America/Indiana/Vevay" />

            // Loop through all mapZone elements
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == System.Xml.XmlNodeType.Element &&
                    xmlReader.Name == "mapZone")
                {
                    string other = xmlReader.GetAttribute("other") ?? string.Empty;
                    string type = xmlReader.GetAttribute("type") ?? string.Empty;

                    // The type attribute can be a space-separated list
                    var typeItems = type.Split(' ');
                    foreach (var typeItem in typeItems)
                    {
                        map.TryAdd(typeItem, other);
                    }
                }
            }

            StringBuilder sb = new();
            foreach (var kvp in map)
            {
                sb.AppendLine($"{kvp.Key} : {kvp.Value}");
            }

            var outputPath = Path.Combine(
                Path.GetDirectoryName(Environment.ProcessPath!)!,
                @"..\..\..\..\..\Assets\Iana2WindowsTimeZoneID.txt");

            var writer = new StreamWriter(File.OpenWrite(outputPath));
            writer.Write(sb.ToString());
            writer.Flush();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in {nameof(CreateCityMapIndex)}: {ex.Message}");
        }
    }


    static string GetSecondFromColonSeparatedValue(string line)
    {
        return line.Split(':')[1].Trim().Trim(',', '"');
    }
}
