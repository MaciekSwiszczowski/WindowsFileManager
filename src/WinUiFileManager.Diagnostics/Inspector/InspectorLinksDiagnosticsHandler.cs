using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Diagnostics-layer handler that answers <see cref="InspectorLinksDiagnosticsRequestMessage"/> with link
/// information for a path: reparse/link target, shell-shortcut detection, and reparse-point status.
/// </summary>
/// <remarks>
/// Lifetime: DI singleton; registers in <see cref="Initialize"/> and unregisters in <see cref="Dispose"/>,
/// but the container is never disposed (AGENTS.md §5), so <see cref="Dispose"/> is effectively unreachable.
/// Threading: answered via <c>message.Reply(Task.Run(...))</c>, so <see cref="Load"/> runs on the thread
/// pool, bounded by <see cref="LoadTimeout"/> linked to the request token. Uses only BCL filesystem APIs.
/// </remarks>
public sealed class InspectorLinksDiagnosticsHandler : IDisposable
{
    private static readonly TimeSpan LoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<InspectorLinksDiagnosticsHandler> _logger;
    private readonly IMessenger _messenger;
    private bool _disposed;

    public InspectorLinksDiagnosticsHandler(
        IMessenger messenger,
        ILogger<InspectorLinksDiagnosticsHandler> logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    /// <summary>Registers the request handler. Not idempotent — call exactly once (AGENTS.md §4).</summary>
    public void Initialize()
    {
        _messenger.Register<InspectorLinksDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    /// <summary>Unregisters from the messenger (idempotent). See type remarks: effectively never called.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>
    /// Reads link/reparse details for the requested path.
    /// </summary>
    /// <param name="message">The request carrying the target path and cancellation token.</param>
    /// <returns>Link details, or <see cref="FileLinkDiagnosticsDetails.Empty"/> on failure.</returns>
    /// <remarks>Thread-pool bound. Real cancellation is rethrown; other errors are logged and degraded to empty.</remarks>
    private FileLinkDiagnosticsDetails Load(InspectorLinksDiagnosticsRequestMessage message)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(message.CancellationToken);
        timeoutCts.CancelAfter(LoadTimeout);

        try
        {
            timeoutCts.Token.ThrowIfCancellationRequested();
            var path = message.Path.DisplayPath;
            FileSystemInfo fileSystemInfo = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            var linkStatus = Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                ? "Shell shortcut"
                : string.Empty;
            var reparseTag = fileSystemInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)
                ? "Reparse point"
                : string.Empty;

            return new FileLinkDiagnosticsDetails(
                fileSystemInfo.LinkTarget ?? string.Empty,
                linkStatus,
                reparseTag,
                string.Empty,
                string.Empty);
        }
        catch (OperationCanceledException) when (message.CancellationToken.IsCancellationRequested)
        {
            // Rethrow only for genuine caller cancellation; timeout cancellation degrades to empty below.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector link diagnostics for {Path}", message.Path.DisplayPath);
            return FileLinkDiagnosticsDetails.Empty;
        }
    }
}
