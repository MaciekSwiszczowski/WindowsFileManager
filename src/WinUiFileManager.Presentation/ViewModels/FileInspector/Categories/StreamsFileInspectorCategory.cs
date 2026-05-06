using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class StreamsFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Streams;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Streams, "Alternate Stream Count", "How many alternate data streams the item has.", 0),
        new(Streams, "Alternate Streams", "Names and sizes of alternate data streams.", 1)
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

            var diagnostics = await fileIdentityService.GetStreamDiagnosticsAsync(selection.FullPath, timeoutCts.Token);
            var hasStreams = !string.IsNullOrWhiteSpace(diagnostics.AlternateStreamCount)
                && diagnostics.AlternateStreamCount != "0";

            return hasStreams
                ? new FileInspectorBatchLoadResult([
                    new FileInspectorFieldUpdate("Alternate Stream Count", diagnostics.AlternateStreamCount),
                    new FileInspectorFieldUpdate(
                        "Alternate Streams",
                        diagnostics.AlternateStreams.Count == 0
                            ? string.Empty
                            : string.Join(Environment.NewLine, diagnostics.AlternateStreams))
                ])
                : new FileInspectorBatchLoadResult([
                    new FileInspectorFieldUpdate("Alternate Stream Count", string.Empty),
                    new FileInspectorFieldUpdate("Alternate Streams", string.Empty)
                ]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load stream diagnostics for {Path}", selection.FullPath);
            return new FileInspectorBatchLoadResult(
            [
                new FileInspectorFieldUpdate("Alternate Stream Count", string.Empty),
                new FileInspectorFieldUpdate("Alternate Streams", string.Empty)
            ]);
        }
    }
}
