using Windows.Storage;

namespace TrayTime.Tests;

/// <summary>
/// Provider to read a StorageFile from the Assets folder during unit tests
/// </summary>
internal class TestAssetProvider : IAssetProvider
{
    async public Task<StorageFile> GetAssetAsync(string assetFilename)
    {
        var productOutputDir = Environment.CurrentDirectory.Replace(
            @"\TrayTime\TrayTime.Tests\bin",
            @"\TrayTime\TrayTime\bin");

        var filePath = Path.Combine(productOutputDir, @"Assets\", assetFilename);

        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
        return file;
    }
}
