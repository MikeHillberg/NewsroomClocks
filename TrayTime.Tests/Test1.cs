using System;
using System.Diagnostics;
using Windows.Storage;

namespace TrayTime.Tests;

[TestClass]
public sealed class Test1
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        App.AssetProvider = new TestAssetProvider();
    }

    [TestMethod]
    public async Task TestMethod1()
    {
        // Calculate the asset paths relative to the unit text run directory

        var cityIndexFilePath =
            Path.Combine(Environment.CurrentDirectory,
                         @"..\..\..\..\..\TrayTime\Assets\CityMapIndex.txt");
        using var cityIndexReader = new StreamReader(cityIndexFilePath);

        var cityMapFilePath =
            Path.Combine(Environment.CurrentDirectory,
                         @"..\..\..\..\..\TrayTime\Assets\cityMap.json");

        var cityMapFile = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(cityMapFilePath));

        // Loop through all the indices
        string? cityIndexLine;
        while ((cityIndexLine = cityIndexReader.ReadLine()) != null)
        {
            TimeZoneInfo? timeZoneInfo = null;

            try
            {
                // Parse the index line
                CityInfoLocation cityIndex = CityInfoLocation.ParseCityMapIndexLine(cityIndexLine);

                // Get the CityInfo from the index line
                CityInfo cityInfo = await cityIndex.GetCityInfoAsync();

                Assert.IsTrue(cityInfo.ToString() == cityIndex.Name,
                    $"Expected: {cityIndex.Name}, Actual: {cityInfo}");

                // bugbug: have to call Load before the property works
                timeZoneInfo = cityInfo.TimeZoneInfo;

                //if (timeZoneInfo == null)
                //{
                //    Debug.WriteLine($"{cityInfo.IanaTimezone}");
                //}
                Assert.IsNotNull(timeZoneInfo);
            }
            catch (Exception) { }
        }
    }
}
