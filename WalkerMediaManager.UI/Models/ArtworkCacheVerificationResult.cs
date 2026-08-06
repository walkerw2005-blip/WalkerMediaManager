using System;

namespace WalkerMediaManager.UI.Models;

public sealed record ArtworkCacheVerificationResult(
    int ValidFiles,
    int RemovedFiles,
    int MissingMarkers,
    TimeSpan Elapsed)
{
    public string Summary =>
        $"Valid artwork files {ValidFiles}; removed invalid or temporary files {RemovedFiles}; " +
        $"active missing-artwork markers {MissingMarkers}; elapsed {Elapsed:hh\\:mm\\:ss}.";
}
