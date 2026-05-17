using System.Reactive.Disposables;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Data;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class SinglePanelView : IDisposable
{
    private readonly PointerEventHandler _panelPointerPressedHandler;
    private CompositeDisposable _dataSourceSubscriptions = new();
    private bool _updatingDriveSelection;
    private bool _loaded;
    private FileEntryTableDataSource? _dataSource;
    private ObservableCollection<SpecFileEntryViewModel>? _items;
    private string? _identity;
    private bool _panelFocused;
    private bool _disposed;

    public SinglePanelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _panelPointerPressedHandler = OnPanelPointerPressed;
        PanelBorder.AddHandler(PointerPressedEvent, _panelPointerPressedHandler, handledEventsToo: true);
        GettingFocus += OnPanelGettingFocus;
        LosingFocus += OnPanelLosingFocus;
    }

    private string Identity => _identity ?? throw new InvalidOperationException("SinglePanel must be initialized with Identity.");

    public PanelViewModel? ViewModel { get; private set; }

    private AppInitializationViewModel? Initialization { get; set; }

    private IMessenger? Messenger { get; set; }

    public SpecFileEntryTableView Table => EntryTable;

    public void Initialize(
        string identity,
        PanelViewModel viewModel,
        IMessenger messenger,
        AppInitializationViewModel initialization)
    {
        if (_identity is not null && _identity != identity)
        {
            throw new InvalidOperationException("Identity cannot be changed once set.");
        }

        _identity = identity;

        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnPanelPropertyChanged;
        }

        ViewModel = viewModel;
        Messenger = messenger;
        Initialization = initialization;

        EntryTable.Identity = Identity;
        EntryTable.Messenger = messenger;
        ViewModel.PropertyChanged += OnPanelPropertyChanged;

        DrivePicker.ItemsSource = Initialization.AvailableVolumes;

        Bindings.Update();
        EnsureFileEntryDataSource();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        EnsureFileEntryDataSource();
        Bindings.Update();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        ViewModel?.PropertyChanged -= OnPanelPropertyChanged;

        _loaded = false;
        _dataSourceSubscriptions.Dispose();
        _dataSourceSubscriptions = [];

        _dataSource?.Dispose();
        _dataSource = null;

        SetItems(null);

        PanelBorder.RemoveHandler(PointerPressedEvent, _panelPointerPressedHandler);
        GettingFocus -= OnPanelGettingFocus;
        LosingFocus -= OnPanelLosingFocus;
    }

    private void EnsureFileEntryDataSource()
    {
        if (!_loaded || Initialization is null || ViewModel is null || _dataSource is not null)
        {
            return;
        }

        var fileSystemService = ViewModel.FileSystemService;
        var uiScheduler = new DispatcherQueueScheduler(DispatcherQueue);

        var initialPath = string.Equals(Identity, "Left", StringComparison.OrdinalIgnoreCase)
            ? Initialization.LeftInitialPath
            : Initialization.RightInitialPath;

        _dataSource?.Dispose();
        _dataSource = new FileEntryTableDataSource(Identity, uiScheduler, fileSystemService, Messenger!);
        _dataSourceSubscriptions.Add(_dataSource.States.Subscribe(ApplyState));

        Messenger?.Send(new FileTableNavigateToPathRequestedMessage(Identity, initialPath));

        // Initial column layout
        Messenger?.Send(new FileTableColumnLayoutMessage(Identity, ColumnLayout.Default));
    }

    private void ApplyState(FileEntryTableDataState state)
    {
        if (ViewModel is not null)
        {
            ViewModel.Items = state.Items;
            ViewModel.CurrentPath = state.CurrentPath;
            ViewModel.ItemCount = state.Items.Count;
            SyncDriveSelection(state.CurrentPath);
        }
    }

    private void SetItems(ObservableCollection<SpecFileEntryViewModel>? items)
    {
        if (ReferenceEquals(_items, items))
        {
            return;
        }
        _items = items;
        _items?.CollectionChanged -= OnItemsCollectionChanged;
        _items?.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ViewModel?.ItemCount = _items?.Count ?? 0;
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

    private async void OnDriveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingDriveSelection
            || sender is not ComboBox { SelectedItem: VolumeInfo volume }
            || ViewModel is null)
        {
            return;
        }

        ViewModel.EditablePath = volume.RootPath.DisplayPath;
        await CommitPathAsync(volume.RootPath.DisplayPath);
    }

    private async void OnPathTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.EditablePath = ViewModel.CurrentPath;
            ViewModel.PathValidationMessage = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await CommitPathAsync(ViewModel.EditablePath);
    }

    private async Task CommitPathAsync(string rawPath)
    {
        if (Messenger is null)
        {
            return;
        }

        Messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, NormalizedPath.FromUserInput(rawPath)));
        ViewModel?.PathValidationMessage = string.Empty;
    }

    private void SyncDriveSelection(string currentPath)
    {
        var volume = FindVolume(currentPath);
        if (ReferenceEquals(DrivePicker.SelectedItem, volume))
        {
            return;
        }

        _updatingDriveSelection = true;
        DrivePicker.SelectedItem = volume;
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

    private void OnPanelPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PublishMessagesOnFocusChanged(isFocused: true);
    }

    private void OnPanelGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: true);
        }
    }

    private void OnPanelLosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (!ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: false);
        }
    }

    private bool ContainsFocusedElement(object? focusedElement)
    {
        var current = focusedElement as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void PublishMessagesOnFocusChanged(bool isFocused)
    {
        if (_panelFocused == isFocused && (!isFocused || ViewModel?.IsActive == true))
        {
            return;
        }

        _panelFocused = isFocused;
        Messenger?.Send(new FileTableFocusedMessage(Identity, isFocused));
        if (isFocused)
        {
            Messenger?.Send(new RefreshInspectorRequestMessage(Identity));
        }
    }
}
