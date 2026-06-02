using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorLinksDiagnosticsRequestMessage"/> with link
/// information for a path: reparse/link target, shell-shortcut detection, and reparse-point status.
/// </summary>
public sealed class InspectorLinksDiagnosticsHandler :
    InspectorDiagnosticsHandlerBase<
        InspectorLinksDiagnosticsRequestMessage,
        FileLinkDiagnosticsDetails,
        InspectorLinksDiagnosticsResponseMessage>
{
    public InspectorLinksDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorLinksDiagnosticsHandler> logger)
        : base(messenger, logger)
    {
    }

    /// <summary>
    /// Reads link/reparse details for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path.</param>
    /// <returns>Link details, or <see cref="FileLinkDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Errors are logged and degraded to empty by the base class.</remarks>
    protected override Task<FileLinkDiagnosticsDetails> LoadAsync(
        InspectorLinksDiagnosticsRequestMessage message,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = message.Path.DisplayPath;
        FileSystemInfo fileSystemInfo = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
        var linkStatus = Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            ? "Shell shortcut"
            : string.Empty;
        var reparseTag = fileSystemInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
            ? "Reparse point"
            : string.Empty;

        return Task.FromResult(new FileLinkDiagnosticsDetails(
            fileSystemInfo.LinkTarget ?? string.Empty,
            linkStatus,
            reparseTag,
            string.Empty,
            string.Empty));
    }

    protected override InspectorLinksDiagnosticsResponseMessage CreateResponse(FileLinkDiagnosticsDetails diagnostics) =>
        new(diagnostics);

    protected override FileLinkDiagnosticsDetails GetEmptyDiagnostics(InspectorLinksDiagnosticsRequestMessage request) =>
        FileLinkDiagnosticsDetails.Empty;
}
