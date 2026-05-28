namespace WinUiFileManager.Presentation.Views;

public sealed partial class PanelsView
{
    public PanelsView()
    {
        InitializeComponent();

        PaneGridSplitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPaneSplitterPointerPressed), handledEventsToo: true);
        Unloaded += OnUnloaded;
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
        => PaneSplitterPressed?.Invoke(this, EventArgs.Empty);

    public PanelsViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(PanelsView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    public event EventHandler? PaneSplitterPressed;

    public void Initialize(PanelsViewModel viewModel)
    {
        ViewModel = viewModel;

        LeftPanel.Initialize(ViewModel.LeftPanel);
        RightPanel.Initialize(ViewModel.RightPanel);

        ViewModel.PropertyChanged += OnPanelsPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= OnPanelsPropertyChanged;
        LeftPanel.Dispose();
        RightPanel.Dispose();
    }

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

    private void FocusActiveTable() => GetActivePanel().Table.Table.Focus(FocusState.Programmatic);

    private SinglePanelView GetActivePanel() =>
        string.Equals(ViewModel.ActivePanelIdentity, "Right", StringComparison.OrdinalIgnoreCase)
            ? RightPanel
            : LeftPanel;

    private void OnPanelsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelsViewModel.ActivePanelIdentity))
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }
}
