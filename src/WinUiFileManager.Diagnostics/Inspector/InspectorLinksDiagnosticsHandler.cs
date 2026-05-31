using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;

namespace WinUiFileManager.Diagnostics.Inspector;

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

    public void Initialize()
    {
        _messenger.Register<InspectorLinksDiagnosticsRequestMessage>(this,
            (_, message) => message.Reply(Task.Run(() => Load(message))));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

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
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load inspector link diagnostics for {Path}", message.Path.DisplayPath);
            return FileLinkDiagnosticsDetails.Empty;
        }
    }
}
