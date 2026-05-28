using System.ComponentModel;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Scheduling;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class SinglePanelView : IDisposable
{
    private readonly PointerEventHandler _panelPointerPressedHandler;
    private bool _loaded;
    private bool _initialNavigationRequested;
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

    public PanelViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(SinglePanelView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    private AppInitializationViewModel? Initialization { get; set; }

    private ObservableCollection<VolumeInfo>? AvailableVolumes => Initialization?.AvailableVolumes;

    private IMessenger? Messenger { get; set; }

    public SpecFileEntryTableView Table => EntryTable;

    public void Initialize(string identity, PanelViewModel viewModel, IMessenger messenger, AppInitializationViewModel initialization)
    {
        if (_identity is not null && _identity != identity)
        {
            throw new InvalidOperationException("Identity cannot be changed once set.");
        }

        _identity = identity;

        Messenger = messenger;
        Initialization = initialization;
        Initialization.PropertyChanged += OnInitializationPropertyChanged;
        ViewModel = viewModel;

        EntryTable.Identity = Identity;
        EntryTable.Messenger = messenger;
        ViewModel.FileEntries.Attach(new DispatcherQueueScheduler(DispatcherQueue));

        EnsureInitialNavigation();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;
        EnsureInitialNavigation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _loaded = false;
        if (Initialization is not null)
        {
            Initialization.PropertyChanged -= OnInitializationPropertyChanged;
        }

        ViewModel.FileEntries.Detach();

        PanelBorder.RemoveHandler(PointerPressedEvent, _panelPointerPressedHandler);
        GettingFocus -= OnPanelGettingFocus;
        LosingFocus -= OnPanelLosingFocus;
    }

    private void EnsureInitialNavigation()
    {
        if (!_loaded || Initialization is null || Messenger is null || _initialNavigationRequested)
        {
            return;
        }

        var initialPath = string.Equals(Identity, "Left", StringComparison.OrdinalIgnoreCase)
            ? Initialization.LeftInitialPath
            : Initialization.RightInitialPath;
        if (string.IsNullOrWhiteSpace(initialPath))
        {
            return;
        }

        _initialNavigationRequested = true;
        Messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, new NormalizedPath(initialPath)));

        // Initial column layout
        Messenger.Send(new FileTableColumnLayoutMessage(Identity, ColumnLayout.Default));
    }

    private void OnInitializationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppInitializationViewModel.LeftInitialPath) or nameof(AppInitializationViewModel.RightInitialPath))
        {
            EnsureInitialNavigation();
        }
    }

    private async void OnDriveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: VolumeInfo volume }
            || IsCurrentPathOnVolume(volume))
        {
            return;
        }

        ViewModel.FileEntries.EditablePath = volume.RootPath.DisplayPath;
        await CommitPathAsync(volume.RootPath.DisplayPath);
    }

    private async void OnPathTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.FileEntries.EditablePath = ViewModel.FileEntries.CurrentPath;
            ViewModel.FileEntries.PathValidationMessage = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await CommitPathAsync(ViewModel.FileEntries.EditablePath);
    }

    private async Task CommitPathAsync(string rawPath)
    {
        if (Messenger is null)
        {
            return;
        }

        Messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, NormalizedPath.FromUserInput(rawPath)));
        ViewModel.FileEntries.PathValidationMessage = string.Empty;
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

    private bool IsCurrentPathOnVolume(VolumeInfo volume)
    {
        var currentPath = ViewModel.FileEntries.CurrentPath;
        return !string.IsNullOrWhiteSpace(currentPath)
            && currentPath.StartsWith(volume.RootPath.DisplayPath, StringComparison.OrdinalIgnoreCase);
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
        if (_panelFocused == isFocused && (!isFocused || ViewModel.IsActive))
        {
            return;
        }

        _panelFocused = isFocused;
        Messenger?.Send(new FileTableFocusedMessage(Identity, isFocused));
        if (isFocused)
        {
            Messenger?.Send(new RefreshInspectorRequestMessage());
        }
    }
}
