namespace WinUiFileManager.Presentation.Views;

public sealed partial class PanelsView
{
    public PanelsView()
    {
        InitializeComponent();

        PaneGridSplitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPaneSplitterPointerPressed), handledEventsToo: true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
        => PaneSplitterPressed?.Invoke(this, EventArgs.Empty);

    public PanelsViewModel? ViewModel { get; private set; }

    public AppInitializationViewModel? Initialization { get; set; }

    public event EventHandler? PaneSplitterPressed;

    public void Initialize(PanelsViewModel viewModel)
    {
        ViewModel = viewModel;

        // var messenger = MessengerProperties.GetMessenger(this)
        //     ?? throw new InvalidOperationException("Messenger must be available.");

        LeftPanel.Initialize(
            ViewModel.LeftPanel.Identity,
            ViewModel.LeftPanel,
            viewModel.Messenger,
            Initialization!);

        RightPanel.Initialize(
            ViewModel.RightPanel.Identity,
            ViewModel.RightPanel,
            viewModel.Messenger,
            Initialization!);

        ViewModel.PropertyChanged += OnPanelsPropertyChanged;
        Bindings.Update();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnPanelsPropertyChanged;
        }

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
        if (ViewModel == null)
        {
            return;
        }

        ViewModel.SetActivePanel(ViewModel.GetOtherPanel().Identity);
        FocusActiveTable();
    }

    private void FocusActiveTable() => GetActivePanel().Table.Table.Focus(FocusState.Programmatic);

    private SinglePanelView GetActivePanel() =>
        string.Equals(ViewModel?.ActivePanelIdentity, "Right", StringComparison.OrdinalIgnoreCase)
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
