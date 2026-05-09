using System.Reactive.Disposables;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Data;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class PanelsView
{
    private CompositeDisposable _dataSourceSubscriptions = new();
    private readonly Dictionary<string, PanelRuntimeState> _panelStates = new(StringComparer.Ordinal);
    private bool _loaded;

    public PanelsView()
    {
        InitializeComponent();

        LeftEntryTable.Identity = "Left";
        RightEntryTable.Identity = "Right";

        PaneGridSplitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPaneSplitterPointerPressed), handledEventsToo: true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public PanelsViewModel? ViewModel { get; private set; }

    public FileEntryTableDataSourceFactory? DataSourceFactory { get; set; }

    public AppInitializationViewModel? Initialization { get; set; }

    public string LeftStatusText => FormatStatus(ViewModel?.LeftPanel);

    public string RightStatusText => FormatStatus(ViewModel?.RightPanel);

    public event EventHandler? PaneSplitterPressed;

    public void Initialize(PanelsViewModel viewModel)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnPanelsPropertyChanged;
            ViewModel.LeftPanel.PropertyChanged -= OnPanelPropertyChanged;
            ViewModel.RightPanel.PropertyChanged -= OnPanelPropertyChanged;
        }

        ViewModel = viewModel;
        _panelStates.Clear();
        _panelStates[ViewModel.LeftPanel.Identity] = new PanelRuntimeState(ViewModel.LeftPanel, LeftEntryTable);
        _panelStates[ViewModel.RightPanel.Identity] = new PanelRuntimeState(ViewModel.RightPanel, RightEntryTable);

        ViewModel.PropertyChanged += OnPanelsPropertyChanged;
        ViewModel.LeftPanel.PropertyChanged += OnPanelPropertyChanged;
        ViewModel.RightPanel.PropertyChanged += OnPanelPropertyChanged;

        Bindings.Update();
        EnsureFileEntryDataSources();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        EnsureFileEntryDataSources();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnPanelsPropertyChanged;
            ViewModel.LeftPanel.PropertyChanged -= OnPanelPropertyChanged;
            ViewModel.RightPanel.PropertyChanged -= OnPanelPropertyChanged;
        }

        _loaded = false;
        _dataSourceSubscriptions.Dispose();
        _dataSourceSubscriptions = new CompositeDisposable();
        foreach (var state in _panelStates.Values)
        {
            state.Dispose();
        }
    }

    private void EnsureFileEntryDataSources()
    {
        if (!_loaded || Initialization is null || _panelStates.Count == 0 || _panelStates.Values.Any(static state => state.DataSource is not null))
        {
            return;
        }

        var factory = DataSourceFactory
            ?? throw new InvalidOperationException("File entry table data source factory is not configured.");
        var uiScheduler = new DispatcherQueueScheduler(DispatcherQueue);

        if (ViewModel == null)
        {
            return;
        }

        CreateDataSource(ViewModel.LeftPanel.Identity, Initialization.LeftInitialPath, factory, uiScheduler);
        CreateDataSource(ViewModel.RightPanel.Identity, Initialization.RightInitialPath, factory, uiScheduler);

        var layout = ColumnLayout.Default;
        WeakReferenceMessenger.Default.Send(new FileTableColumnLayoutMessage(ViewModel.LeftPanel.Identity, layout));
        WeakReferenceMessenger.Default.Send(new FileTableColumnLayoutMessage(ViewModel.RightPanel.Identity, layout));
    }

    private void CreateDataSource(
        string panelIdentity,
        NormalizedPath initialPath,
        FileEntryTableDataSourceFactory factory,
        DispatcherQueueScheduler uiScheduler)
    {
        var panel = _panelStates[panelIdentity];
        var dataSource = factory.Create(panelIdentity, initialPath.DisplayPath, uiScheduler);
        panel.DataSource = dataSource;

        _dataSourceSubscriptions.Add(dataSource.States.Subscribe(state => ApplyState(panel, state)));
    }

    private void ApplyState(PanelRuntimeState panel, FileEntryTableDataState state)
    {
        panel.SetItems(state.Items);
        panel.Table.ItemsSource = state.Items;
        panel.ViewModel.CurrentPath = state.CurrentPath;
        panel.ViewModel.ItemCount = state.Items.Count;
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

    private void FocusActiveTable() => GetActiveTable().Focus(FocusState.Programmatic);

    private TableView GetActiveTable() => _panelStates[ViewModel!.ActivePanelIdentity].Table.Table;

    private void OnPanelsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelsViewModel.ActivePanelIdentity))
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanelViewModel.IsActive)
            or nameof(PanelViewModel.CurrentPath)
            or nameof(PanelViewModel.ItemCount)
            or nameof(PanelViewModel.SelectedCount))
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PaneSplitterPressed?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatStatus(PanelViewModel? panel) =>
        $"{panel?.ItemCount} items    {panel?.SelectedCount} selected";

    private sealed class PanelRuntimeState : IDisposable
    {
        private ObservableCollection<SpecFileEntryViewModel>? _items;

        public PanelRuntimeState(PanelViewModel viewModel, SpecFileEntryTableView table)
        {
            ViewModel = viewModel;
            Table = table;
        }

        public PanelViewModel ViewModel { get; }

        public SpecFileEntryTableView Table { get; }

        public FileEntryTableDataSource? DataSource { get; set; }

        public void SetItems(ObservableCollection<SpecFileEntryViewModel>? items)
        {
            if (ReferenceEquals(_items, items))
            {
                return;
            }

            if (_items is not null)
            {
                _items.CollectionChanged -= OnItemsCollectionChanged;
            }

            _items = items;

            if (_items is not null)
            {
                _items.CollectionChanged += OnItemsCollectionChanged;
            }
        }

        public void Dispose()
        {
            DataSource?.Dispose();
            DataSource = null;
            SetItems(null);
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ViewModel.ItemCount = _items?.Count ?? 0;
        }
    }
}
