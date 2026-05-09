using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Messages;

namespace WinUiFileManager.Infrastructure.Services;

public sealed class ActivePanelsService : IActivePanelsService, IDisposable
{
    private string _activePanelIdentity = "Left";
    private bool _disposed;
    private string _targetPanelIdentity = "Right";

    public ActivePanelsService()
    {
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    public string ActivePanelIdentity => _activePanelIdentity;

    public string TargetPanelIdentity => _targetPanelIdentity;

    public void SetActivePanel(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || string.Equals(identity, _activePanelIdentity, StringComparison.Ordinal))
        {
            return;
        }

        _targetPanelIdentity = _activePanelIdentity;
        _activePanelIdentity = identity;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            _activePanelIdentity = message.Identity;
        }
        else
        {
            _targetPanelIdentity = message.Identity;
        }
    }
}
