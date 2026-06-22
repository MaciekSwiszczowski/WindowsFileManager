using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Diagnostics.Profiling;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Diagnostics.Inspector;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorDiagnosticsRequestMessage"/> by extracting a Shell
/// thumbnail for the requested path via the Win32 Shell imaging COM path (<see cref="IShellThumbnailInterop"/>) and
/// returning it as raw BGRA pixels plus the file's extension association.
/// </summary>
/// <remarks>
/// <para>
/// Replaces the former WinRT <see cref="P:Windows.Storage.StorageFile"/>/<c>GetThumbnailAsync</c> path: that path
/// created several non-disposable runtime-callable wrappers per selection whose native COM was released only by the
/// GC finalizer. The Win32 path uses deterministic COM release and creates no such wrappers.
/// </para>
/// <para>
/// <see cref="IShellThumbnailInterop.TryGetThumbnail"/> is synchronous; <see cref="LoadAsync"/> already runs on the
/// base class's <c>Task.Run</c> thread-pool thread, so calling it inline does not block the UI. The synchronous
/// Shell call is not cancellable, so this handler relies on the base's latest-request-wins suppression rather than a
/// timeout.
/// </para>
/// </remarks>
public sealed class InspectorThumbnailDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        FileThumbnailDiagnosticsDetails,
        InspectorThumbnailDiagnosticsResponseMessage>
{
    // 48px keeps the BGRA buffer (48*48*4 = 9KB) on the SOH, well under the 85KB large-object-heap threshold, and
    // is about one inspector row tall — sharp enough for the preview without churning/fragmenting the LOH.
    private const uint ThumbnailSize = 48;

    private readonly IShellThumbnailInterop _shellThumbnails;

    public InspectorThumbnailDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorThumbnailDiagnosticsHandler> logger,
        Func<FileThumbnailDiagnosticsDetails, InspectorThumbnailDiagnosticsResponseMessage> responseFactory,
        IInspectorDiagnosticsGate diagnosticsGate,
        IShellThumbnailInterop shellThumbnails)
        : base(messenger, logger, responseFactory, diagnosticsGate)
    {
        _shellThumbnails = shellThumbnails;
    }

    protected override DiagnosticsCategory Category => DiagnosticsCategory.Thumbnail;

    /// <summary>
    /// Extracts a 48px thumbnail for the path as BGRA pixels via the Win32 Shell imaging path.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>Thumbnail pixels and dimensions, or null pixels with just the extension when none is available.</returns>
    /// <remarks>Thread-pool bound (runs on the base's <c>Task.Run</c> thread). Failures degrade to empty.</remarks>
    protected override Task<FileThumbnailDiagnosticsDetails> LoadAsync(InspectorDiagnosticsRequestMessage message)
    {
        var path = message.Path.DisplayPath;
        var progId = Path.GetExtension(path);

        var diagnostics = _shellThumbnails.TryGetThumbnail(path, ThumbnailSize, out var thumbnail)
            ? new FileThumbnailDiagnosticsDetails(thumbnail.Pixels, thumbnail.Width, thumbnail.Height, progId)
            : new FileThumbnailDiagnosticsDetails(null, 0, 0, progId);

        return Task.FromResult(diagnostics);
    }

    protected override FileThumbnailDiagnosticsDetails GetEmptyDiagnostics(InspectorDiagnosticsRequestMessage request) =>
        FileThumbnailDiagnosticsDetails.Empty;
}
