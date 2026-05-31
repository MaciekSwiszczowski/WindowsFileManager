using WinUiFileManager.Application.Messages.RequestMessages;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// View model for the command-bar buttons. Translates button clicks into key-pressed request messages (copy,
/// move, rename, delete, create folder, copy path) and owns the inspector-visibility / parallel-execution toggle
/// state and the theme-toggle callback.
/// </summary>
/// <remarks>
/// Messaging: registers <see cref="ToggleInspectorKeyPressedMessage"/> and <see cref="ToggleInspectorRequestedMessage"/>
/// against the strong-reference messenger; <see cref="Dispose"/> unregisters them. Note the deliberate two-way
/// inspector toggle loop: external <see cref="ToggleInspectorRequestedMessage"/> updates <see cref="IsInspectorVisible"/>,
/// and a UI-driven change to <see cref="IsInspectorVisible"/> re-broadcasts the same message
/// (<see cref="OnIsInspectorVisibleChanged"/>) — the equality short-circuit on the property prevents a feedback storm.
/// </remarks>
public sealed partial class CommandButtonsViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private bool _disposed;

    public CommandButtonsViewModel(IMessenger messenger)
    {
        _messenger = messenger;
        _messenger.Register<ToggleInspectorKeyPressedMessage>(this, OnToggleInspectorKeyPressed);
        _messenger.Register<ToggleInspectorRequestedMessage>(this, OnToggleInspectorRequested);
    }

    /// <summary>Callback invoked by the theme button; set by the host (code-behind) that owns theme switching.</summary>
    public Action? ToggleThemeAction { get; set; }

    /// <summary>Inspector visibility toggle state; changing it broadcasts <see cref="ToggleInspectorRequestedMessage"/>.</summary>
    [ObservableProperty]
    public partial bool IsInspectorVisible { get; set; } = true;

    /// <summary>Parallel-execution toggle state mirrored from settings for the command bar.</summary>
    [ObservableProperty]
    public partial bool ParallelExecutionEnabled { get; set; }

    /// <summary>App-wide messenger, exposed for bindings/behaviors.</summary>
    public IMessenger Messenger => _messenger;

    /// <summary>Unregisters messenger recipients. Idempotent via <see cref="_disposed"/>.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    /// <summary>Handles the keyboard toggle shortcut by flipping <see cref="IsInspectorVisible"/>.</summary>
    private void OnToggleInspectorKeyPressed(object recipient, ToggleInspectorKeyPressedMessage _)
    {
        IsInspectorVisible = !IsInspectorVisible;
    }

    /// <summary>Handles an explicit visibility request (e.g. from the shell) by setting <see cref="IsInspectorVisible"/>.</summary>
    private void OnToggleInspectorRequested(object recipient, ToggleInspectorRequestedMessage message)
    {
        IsInspectorVisible = message.IsVisible;
    }

    /// <summary>Re-broadcasts visibility changes so other recipients (shell, inspector) stay in sync.</summary>
    partial void OnIsInspectorVisibleChanged(bool value)
    {
        _messenger.Send(new ToggleInspectorRequestedMessage(value));
    }

    // Each command forwards a UI action as a *KeyPressed request message; the actual handlers live elsewhere
    // (table behaviors / file-operation handlers), keeping this VM a thin message dispatcher.

    [RelayCommand]
    private void Copy() => _messenger.Send(new CopyKeyPressedMessage());

    [RelayCommand]
    private void Move() => _messenger.Send(new MoveKeyPressedMessage());

    [RelayCommand]
    private void Rename() => _messenger.Send(new RenameKeyPressedMessage());

    [RelayCommand]
    private void Delete() => _messenger.Send(new DeleteKeyPressedMessage());

    [RelayCommand]
    private void CreateFolder() => _messenger.Send(new CreateFolderKeyPressedMessage());

    [RelayCommand]
    private void CopyPath()
    {
        _messenger.Send(new CopyPathKeyPressedMessage());
    }

    [RelayCommand]
    private void ToggleTheme() => ToggleThemeAction?.Invoke();
}
