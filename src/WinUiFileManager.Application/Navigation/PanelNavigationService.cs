using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Application.Navigation;

#pragma warning disable MA0026
/// <summary>
/// Coordinates panel navigation requests and keeps the current path for each panel identity.
/// TODO:
/// - Replace synchronous path existence checks with an investigated non-blocking API.
/// - Decide user-facing error messages for missing paths, security errors, and admin-required navigation.
/// - Serve current panel paths for application-state serialization on close.
/// - Store volume identity so inactive panels can detect volume changes before reloading.
/// </summary>
#pragma warning restore MA0026
public sealed class PanelNavigationService : IDisposable
{
    private readonly Dictionary<string, NormalizedPath> _currentPaths = new(StringComparer.Ordinal);
    private readonly IMessenger _messenger;
    private bool _disposed;

    public PanelNavigationService(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register<FileTableNavigateToPathRequestedMessage>(this, OnNavigateToPathRequested);
        _messenger.Register<FileTableNavigateUpRequestedMessage>(this, OnNavigateUpRequested);
        _messenger.Register<FileTableNavigateDownRequestedMessage>(this, OnNavigateDownRequested);
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

    private void OnNavigateToPathRequested(object recipient, FileTableNavigateToPathRequestedMessage message)
    {
        TryNavigate(message.Identity, message.Path);
    }

    private void OnNavigateUpRequested(object recipient, FileTableNavigateUpRequestedMessage message)
    {
        if (!_currentPaths.TryGetValue(message.Identity, out var currentPath))
        {
            return;
        }

        var parent = Directory.GetParent(currentPath.DisplayPath);
        if (parent is null)
        {
            return;
        }

        TryNavigate(message.Identity, NormalizedPath.FromUserInput(parent.FullName));
    }

    private void OnNavigateDownRequested(object recipient, FileTableNavigateDownRequestedMessage message)
    {
        if (!_currentPaths.TryGetValue(message.Identity, out var currentPath)
            || string.IsNullOrWhiteSpace(message.FolderName))
        {
            return;
        }

        var childPath = new NormalizedPath(Path.Combine(currentPath.Value, message.FolderName));
        TryNavigate(message.Identity, childPath);
    }

    private void TryNavigate(string identity, NormalizedPath path)
    {
        if (!Directory.Exists(path.DisplayPath))
        {
            return;
        }

        _currentPaths[identity] = path;
        _messenger.Send(new FileTableNavigateToPathMessage(identity, path));
    }
}
