using Windows.Storage;

namespace TrayTime.Tests;

/// <summary>
/// Provider to read a StorageFile from the Assets folder during unit tests
/// </summary>
internal class TestAssetProvider : IAssetProvider
{
    async public Task<StorageFile> GetAssetAsync(string assetPath)
    {
        // Location of the assets relative to where the tests run
        var filePath = Path.Combine(Environment.CurrentDirectory,
                                    $@"..\..\..\..\..\TrayTime\Assets\{assetPath}");

        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(filePath));
        return file;
    }
}
