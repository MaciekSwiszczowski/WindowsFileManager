using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class IdentityFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Ids;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Ids, "File ID", "128-bit NTFS identifier for the selected file system entry.", 0),
        new(Ids, "Volume Serial", "Volume serial number of the drive that contains the item.", 1),
        new(Ids, "File Index (64-bit)", "Older 64-bit file index from the legacy Windows API. Diagnostic/compatibility value only.", 2),
        new(Ids, "Hard Link Count", "How many hard links point to the same file record, when available.", 3),
        new(Ids, "Final Path", "The resolved final path reported by Windows.", 4)
    ];

    public static async Task<FileInspectorBatchLoadResult> LoadAsync(
        IFileIdentityService fileIdentityService,
        ILogger<FileInspectorViewModel> logger,
        FileInspectorSelection selection,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            var ntfsDetails = await fileIdentityService.GetNtfsMetadataDetailsAsync(selection.FullPath, timeoutCts.Token);
            var details = await fileIdentityService.GetIdentityDetailsAsync(selection.FullPath, timeoutCts.Token);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Created", FileInspectorFormatting.RequiredUtc(ntfsDetails.CreationTimeUtc)),
                new FileInspectorFieldUpdate("Accessed", FileInspectorFormatting.RequiredUtc(ntfsDetails.LastAccessTimeUtc)),
                new FileInspectorFieldUpdate("Modified", FileInspectorFormatting.RequiredUtc(ntfsDetails.LastWriteTimeUtc)),
                new FileInspectorFieldUpdate("MFT Changed", FileInspectorFormatting.RequiredUtc(ntfsDetails.ChangeTimeUtc)),
                new FileInspectorFieldUpdate(
                    "File ID",
                    details.FileId == NtfsFileId.None ? "Unavailable" : details.FileId.HexDisplay),
                new FileInspectorFieldUpdate("Volume Serial", details.VolumeSerial),
                new FileInspectorFieldUpdate("File Index (64-bit)", details.LegacyFileIndex),
                new FileInspectorFieldUpdate("Hard Link Count", details.HardLinkCount),
                new FileInspectorFieldUpdate("Final Path", details.FinalPath)
            ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load identity details for {Path}", selection.FullPath);
            return new FileInspectorBatchLoadResult([new FileInspectorFieldUpdate("File ID", "Unavailable")]);
        }
    }
}
