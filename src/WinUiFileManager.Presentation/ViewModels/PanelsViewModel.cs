using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// Container view model for the dual-pane layout. Owns the left/right <see cref="PanelViewModel"/>s, tracks which
/// pane is active (delegating the authoritative state to <see cref="IActivePanelsService"/>), and keeps active/
/// selection state in sync in response to focus and selection messages.
/// </summary>
/// <remarks>
/// <para>Lifetime: one instance per shell, created on the UI/dispatcher thread (enforced in the constructor).</para>
/// <para>
/// Messaging: <see cref="Initialize"/> registers <see cref="FileTableFocusedMessage"/> and
/// <see cref="FileTableSelectionChangedMessage"/> against the strong-reference messenger; <see cref="Dispose"/>
/// must run to unregister and to cascade disposal to both child panels (it does).
/// </para>
/// </remarks>
public sealed partial class PanelsViewModel : ObservableObject, IDisposable
{
    private readonly IActivePanelsService _activePanelsService;
    private readonly UiDispatcherQueue _dispatcherQueue;
    private readonly IMessenger _messenger;
    private bool _disposed;

    /// <summary>
    /// Creates the container and both panes via <paramref name="panelFactory"/>. Must run on a dispatcher (UI)
    /// thread; throws otherwise, because <see cref="SetActivePanel"/> marshals onto the captured queue.
    /// </summary>
    /// <param name="panelFactory">Factory keyed by pane identity (<c>"Left"</c>/<c>"Right"</c>).</param>
    /// <exception cref="InvalidOperationException">Thrown when constructed off a dispatcher thread.</exception>
    public PanelsViewModel(
        IActivePanelsService activePanelsService,
        IMessenger messenger,
        Func<string, PanelViewModel> panelFactory)
    {
        _activePanelsService = activePanelsService;
        _messenger = messenger;
        _dispatcherQueue = UiDispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException($"{nameof(PanelsViewModel)} must be created on a dispatcher thread.");
        LeftPanel = panelFactory("Left");
        RightPanel = panelFactory("Right");
    }

    /// <summary>
    /// Initializes both panes and registers focus/selection recipients. Not guarded against double-invocation
    /// here; call once after construction. (The child panes guard their own re-initialization.)
    /// </summary>
    public void Initialize()
    {
        LeftPanel.Initialize();
        RightPanel.Initialize();
        _messenger.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
        _messenger.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
        SetActivePanel(_activePanelsService.ActivePanelIdentity);
    }

    /// <summary>The left pane view model (identity <c>"Left"</c>).</summary>
    public PanelViewModel LeftPanel { get; }

    /// <summary>The right pane view model (identity <c>"Right"</c>).</summary>
    public PanelViewModel RightPanel { get; }

    /// <summary>Width (DIPs) of the left pane's grid column; the right pane takes the remainder.</summary>
    [ObservableProperty]
    public partial double LeftPanelWidth { get; set; } = 600d;

    /// <summary>Identity of the active pane, read from the authoritative <see cref="IActivePanelsService"/>.</summary>
    public string ActivePanelIdentity => _activePanelsService.ActivePanelIdentity;

    /// <summary>The currently active <see cref="PanelViewModel"/>.</summary>
    public PanelViewModel ActivePanel => GetPanel(ActivePanelIdentity);

    /// <summary>
    /// Makes the pane with the given identity active and updates each pane's <see cref="PanelViewModel.IsActive"/>.
    /// UI-thread affine: re-enqueues itself onto the dispatcher when called off-thread (and throws if it cannot
    /// enqueue), since it raises change notifications consumed by the view.
    /// </summary>
    /// <param name="identity">Target pane identity; unknown values resolve to the left pane.</param>
    /// <exception cref="InvalidOperationException">Thrown when the dispatcher cannot accept the marshalled call.</exception>
    public void SetActivePanel(string identity)
    {
        if (!_dispatcherQueue.HasThreadAccess)
        {
            if (!_dispatcherQueue.TryEnqueue(() => SetActivePanel(identity)))
            {
                throw new InvalidOperationException("Failed to enqueue active panel update on the UI dispatcher.");
            }

            return;
        }

        var panel = GetPanel(identity);
        _activePanelsService.SetActivePanel(panel.Identity);
        OnPropertyChanged(nameof(ActivePanelIdentity));
        OnPropertyChanged(nameof(ActivePanel));
        LeftPanel.IsActive = string.Equals(LeftPanel.Identity, ActivePanelIdentity, StringComparison.Ordinal);
        RightPanel.IsActive = string.Equals(RightPanel.Identity, ActivePanelIdentity, StringComparison.Ordinal);
    }

    /// <summary>Resolves an identity to a pane, defaulting to <see cref="LeftPanel"/> for anything not the right pane.</summary>
    private PanelViewModel GetPanel(string identity) =>
        string.Equals(identity, RightPanel.Identity, StringComparison.OrdinalIgnoreCase)
            ? RightPanel
            : LeftPanel;

    /// <summary>Returns the pane that is <i>not</i> currently active (used for cross-pane operations).</summary>
    public PanelViewModel GetOtherPanel() =>
        string.Equals(ActivePanelIdentity, LeftPanel.Identity, StringComparison.Ordinal)
            ? RightPanel
            : LeftPanel;

    /// <summary>
    /// Unregisters messenger recipients and disposes both child panes. Idempotent via <see cref="_disposed"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
        LeftPanel.Dispose();
        RightPanel.Dispose();
    }

    /// <summary>Handles <see cref="FileTableFocusedMessage"/>: a focused table promotes its pane to active.</summary>
    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.IsFocused)
        {
            SetActivePanel(message.Identity);
        }
    }

    /// <summary>Handles <see cref="FileTableSelectionChangedMessage"/>: mirrors the selected count onto the source pane.</summary>
    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        GetPanel(message.Identity).SelectedCount = message.SelectedItems.Count;
    }
}
