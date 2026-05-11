using System.Reactive.Disposables;
using WinUiFileManager.Application.NavigationMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Data;
using WinUiFileManager.Presentation.Messaging;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class PanelsView
{
    private CompositeDisposable _dataSourceSubscriptions = new();
    private readonly Dictionary<string, PanelRuntimeState> _panelStates = new(StringComparer.Ordinal);
    private bool _updatingDriveSelection;
    private bool _loaded;

    public PanelsView()
    {
        InitializeComponent();

        RegisterPropertyChangedCallback(MessengerProperties.MessengerProperty, OnPanelsMessengerChanged);
        LeftEntryTable.Identity = "Left";
        RightEntryTable.Identity = "Right";

        PaneGridSplitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPaneSplitterPointerPressed), handledEventsToo: true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnPanelsMessengerChanged(DependencyObject sender, DependencyProperty dp) => TrySendInitialColumnLayout();

    public PanelsViewModel? ViewModel { get; private set; }

    public FileEntryTableDataSourceFactory? DataSourceFactory { get; set; }

    public AppInitializationViewModel? Initialization { get; set; }

    public GoToPathCommandHandler? GoToPathCommandHandler { get; set; }

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

        SetDrivePickerItemsSources();
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
        if (!_loaded || Initialization is null || _panelStates.Count == 0)
        {
            return;
        }

        var hasAnyDataSource = _panelStates.Values.Any(static state => state.DataSource is not null);
        if (!hasAnyDataSource)
        {
            var factory = DataSourceFactory
                ?? throw new InvalidOperationException("File entry table data source factory is not configured.");
            var uiScheduler = new DispatcherQueueScheduler(DispatcherQueue);

            if (ViewModel == null)
            {
                return;
            }

            CreateDataSource(ViewModel.LeftPanel.Identity, Initialization.LeftInitialPath, factory, uiScheduler);
            CreateDataSource(ViewModel.RightPanel.Identity, Initialization.RightInitialPath, factory, uiScheduler);
        }

        TrySendInitialColumnLayout();
    }

    private void SetDrivePickerItemsSources()
    {
        LeftDrivePicker.ItemsSource = Initialization?.AvailableVolumes;
        RightDrivePicker.ItemsSource = Initialization?.AvailableVolumes;
    }

    private void TrySendInitialColumnLayout()
    {
        if (MessengerProperties.GetMessenger(this) is not { } messenger || ViewModel is null)
        {
            return;
        }

        if (_panelStates.Values.Any(static state => state.DataSource is null))
        {
            return;
        }

        var layout = ColumnLayout.Default;
        messenger.Send(new FileTableColumnLayoutMessage(ViewModel.LeftPanel.Identity, layout));
        messenger.Send(new FileTableColumnLayoutMessage(ViewModel.RightPanel.Identity, layout));
    }

    private void CreateDataSource(
        string panelIdentity,
        NormalizedPath initialPath,
        FileEntryTableDataSourceFactory factory,
        DispatcherQueueScheduler uiScheduler)
    {
        var panel = _panelStates[panelIdentity];
        var dataSource = factory.Create(panelIdentity, uiScheduler);
        panel.DataSource = dataSource;

        _dataSourceSubscriptions.Add(dataSource.States.Subscribe(state => ApplyState(panel, state)));
        ViewModel?.Messenger.Send(new FileTableNavigateToPathRequestedMessage(panelIdentity, initialPath));
    }

    private void ApplyState(PanelRuntimeState panel, FileEntryTableDataState state)
    {
        panel.SetItems(state.Items);
        panel.Table.ItemsSource = state.Items;
        panel.ViewModel.CurrentPath = state.CurrentPath;
        panel.ViewModel.ItemCount = state.Items.Count;
        SyncDriveSelection(panel, state.CurrentPath);
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
            or nameof(PanelViewModel.SelectedCount)
            or nameof(PanelViewModel.PathValidationMessage))
        {
            DispatcherQueue.TryEnqueue(Bindings.Update);
        }
    }

    private void OnPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PaneSplitterPressed?.Invoke(this, EventArgs.Empty);
    }

    private async void OnDriveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingDriveSelection
            || sender is not ComboBox { SelectedItem: VolumeInfo volume }
            || !TryGetPanel(sender, out var panel))
        {
            return;
        }

        panel.ViewModel.EditablePath = volume.RootPath.DisplayPath;
        await CommitPathAsync(panel, volume.RootPath.DisplayPath);
    }

    private async void OnPathTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!TryGetPanel(sender, out var panel))
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            panel.ViewModel.EditablePath = panel.ViewModel.CurrentPath;
            panel.ViewModel.PathValidationMessage = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await CommitPathAsync(panel, panel.ViewModel.EditablePath);
    }

    private async Task CommitPathAsync(PanelRuntimeState panel, string rawPath)
    {
        if (GoToPathCommandHandler is null || ViewModel is null)
        {
            return;
        }

        var result = await GoToPathCommandHandler.ValidateDirectoryAsync(rawPath, CancellationToken.None);
        if (!result.Success || result.Path is not { } normalizedPath)
        {
            panel.ViewModel.PathValidationMessage = result.ErrorMessage ?? "Invalid path.";
            return;
        }

        ViewModel.Messenger.Send(new FileTableNavigateToPathRequestedMessage(panel.ViewModel.Identity, normalizedPath));
        panel.ViewModel.PathValidationMessage = string.Empty;
    }

    private bool TryGetPanel(object sender, out PanelRuntimeState panel)
    {
        if (ViewModel is null)
        {
            panel = default!;
            return false;
        }

        if (ReferenceEquals(sender, LeftDrivePicker) || ReferenceEquals(sender, LeftPathBox))
        {
            panel = _panelStates[ViewModel.LeftPanel.Identity];
            return true;
        }

        if (ReferenceEquals(sender, RightDrivePicker) || ReferenceEquals(sender, RightPathBox))
        {
            panel = _panelStates[ViewModel.RightPanel.Identity];
            return true;
        }

        panel = default!;
        return false;
    }

    private void SyncDriveSelection(PanelRuntimeState panel, string currentPath)
    {
        var drivePicker = ReferenceEquals(panel.ViewModel, ViewModel?.LeftPanel)
            ? LeftDrivePicker
            : RightDrivePicker;

        var volume = FindVolume(currentPath);
        if (ReferenceEquals(drivePicker.SelectedItem, volume))
        {
            return;
        }

        _updatingDriveSelection = true;
        drivePicker.SelectedItem = volume;
        _updatingDriveSelection = false;
    }

    private VolumeInfo? FindVolume(string path)
    {
        if (Initialization is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var volume in Initialization.AvailableVolumes)
        {
            var root = volume.RootPath.DisplayPath;
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return volume;
            }
        }

        return null;
    }

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
