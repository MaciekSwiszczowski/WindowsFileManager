namespace WinUiFileManager.Presentation.Views;

/// <summary>
/// Code-behind for the dual-pane container: hosts the left/right <see cref="SinglePanelView"/>s, raises
/// <see cref="PaneSplitterPressed"/> when the pane splitter is grabbed, and implements Tab to switch the
/// active pane and move focus to its table.
/// </summary>
/// <remarks>
/// Owns the disposal of its two child panels: <see cref="OnUnloaded"/> reverses the VM subscription and
/// calls <c>Dispose()</c> on both panels, which is the link in the disposal chain that lets each panel's
/// data source / messenger registrations be released. The splitter <c>AddHandler</c> and
/// <c>Unloaded</c> subscriptions in the constructor are not reversed, but are self-targeted and
/// collected with the view (consistent with the other views; AGENTS.md §5).
/// </remarks>
public sealed partial class PanelsView
{
    public PanelsView()
    {
        InitializeComponent();

        // handledEventsToo so the splitter's own handling of the press doesn't hide it from us.
        PaneGridSplitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPaneSplitterPointerPressed), handledEventsToo: true);
        Unloaded += OnUnloaded;
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
        => PaneSplitterPressed?.Invoke(this, EventArgs.Empty);

    /// <summary>The bound view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before <see cref="Initialize"/>.</exception>
    public PanelsViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(PanelsView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    /// <summary>Raised when the user grabs the pane splitter; the shell uses this to freeze table
    /// layout for the duration of the drag.</summary>
    public event EventHandler? PaneSplitterPressed;

    /// <summary>Wires the view to its view model and initialises both child panels. Must be called once
    /// after construction.</summary>
    public void Initialize(PanelsViewModel viewModel)
    {
        ViewModel = viewModel;

        LeftPanel.Initialize(ViewModel.LeftPanel);
        RightPanel.Initialize(ViewModel.RightPanel);

        ViewModel.PropertyChanged += OnPanelsPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Balance the Initialize subscription, then dispose the child panels so their data sources and
        // messenger registrations are released (part of the disposal chain; AGENTS.md §5).
        ViewModel.PropertyChanged -= OnPanelsPropertyChanged;
        LeftPanel.Dispose();
        RightPanel.Dispose();
    }

    /// <summary>Tab (without Control) switches focus to the other pane; Ctrl+Tab is left for other use.
    /// UI-thread key handler.</summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Tab || WinUiViewHelper.IsModifierDown(VirtualKey.Control))
        {
            return;
        }

        FocusOtherPanel();
        e.Handled = true;
    }

    private void FocusOtherPanel()
    {
        ViewModel.SetActivePanel(ViewModel.GetOtherPanel().Identity);
        FocusActiveTable();
    }

    /// <summary>Moves keyboard focus to the active pane's table programmatically.</summary>
    private void FocusActiveTable() => GetActivePanel().Table.Table.Focus(FocusState.Programmatic);

    private SinglePanelView GetActivePanel() =>
        string.Equals(ViewModel.ActivePanelIdentity, "Right", StringComparison.OrdinalIgnoreCase)
            ? RightPanel
            : LeftPanel;

    // Active-pane changes may originate off the UI thread; marshal the binding refresh to the dispatcher.
    private void OnPanelsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelsViewModel.ActivePanelIdentity))
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }
}
