using Windows.Storage;

namespace NewsroomClocks.Tests;

/// <summary>
/// Provider to read a StorageFile from the Assets folder during unit tests
/// </summary>
internal class TestAssetProvider : IAssetProvider
{
    async public Task<StorageFile> GetAssetAsync(string assetFilename)
    {
        var productOutputDir = Environment.CurrentDirectory.Replace(
            @"\NewsroomClocks\NewsroomClocks.Tests\bin",
            @"\NewsroomClocks\NewsroomClocks\bin");

        var filePath = Path.Combine(productOutputDir, @"win-x64\Assets\", assetFilename);

        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
        return file;
    }
}
