using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace NewsroomClocks;

/// <summary>
/// Read from the Assets folder as a StorageFile
/// </summary>
public interface IAssetProvider
{
    Task<StorageFile> GetAssetAsync(string assetPath);
}

/// <summary>
/// Read from the Assets folder as a StorageFile, for use in app code (not test)
/// </summary>
internal class AssetProvider : IAssetProvider
{
    async public Task<StorageFile> GetAssetAsync(string assetName)
    {
        // Running the app, assets are in the msix package
        var uri = new Uri($"ms-appx:///Assets/{assetName}");
        var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
        return file;
    }
}
