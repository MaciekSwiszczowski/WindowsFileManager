using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Messages;

namespace WinUiFileManager.Infrastructure.Services;

/// <summary>
/// Tracks which of the two file-manager panes is currently active (focused) and which is the "target" (the other
/// pane, used as the destination for cross-pane operations). Updates itself by listening for
/// <see cref="FileTableFocusedMessage"/> on the app-wide messenger, and also accepts explicit updates via
/// <see cref="SetActivePanel"/>. Infrastructure implementation of <see cref="IActivePanelsService"/>.
/// </summary>
/// <remarks>
/// MESSENGER LIFETIME (AGENTS.md §4/§5): registers against the shared <see cref="StrongReferenceMessenger"/> in
/// <see cref="Initialize"/>, which roots this instance until <see cref="Dispose"/> calls <c>UnregisterAll</c>.
/// As a DI singleton, that <see cref="Dispose"/> only runs if the container is disposed on shutdown.
/// <see cref="Dispose"/> is idempotent. Identity values are the pane identity strings ("Left"/"Right").
/// </remarks>
public sealed class ActivePanelsService : IActivePanelsService, IDisposable
{
    private readonly IMessenger _messenger;
    private string _activePanelIdentity = "Left";
    private bool _disposed;
    private string _targetPanelIdentity = "Right";

    public ActivePanelsService(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _messenger = messenger;
    }

    /// <summary>
    /// Subscribes to focus changes. Called once by the composition root after resolution.
    /// </summary>
    /// <remarks>
    /// NOT guarded for re-entry: calling this more than once would double-register the handler and double-handle
    /// messages (see AGENTS.md §4). Invoke exactly once.
    /// </remarks>
    public void Initialize()
    {
        _messenger.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
    }

    /// <summary>The identity of the currently active (focused) pane.</summary>
    public string ActivePanelIdentity => _activePanelIdentity;

    /// <summary>The identity of the target pane (the non-active pane; the default destination for operations).</summary>
    public string TargetPanelIdentity => _targetPanelIdentity;

    /// <summary>Explicitly promotes <paramref name="identity"/> to active, demoting the previous active pane to target.</summary>
    /// <param name="identity">The pane identity to activate. No-op if blank or already active.</param>
    public void SetActivePanel(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || string.Equals(identity, _activePanelIdentity, StringComparison.Ordinal))
        {
            return;
        }

        // The pane that was active becomes the target, so the two identities always describe distinct panes.
        _targetPanelIdentity = _activePanelIdentity;
        _activePanelIdentity = identity;
    }

    /// <summary>Unregisters from the messenger. Idempotent; required to avoid rooting this instance forever (§4).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    // A pane reports focus gained (becomes active) or lost (becomes the target). Unlike SetActivePanel this does
    // not swap both fields: it trusts the focus notifications to keep active/target consistent.
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
