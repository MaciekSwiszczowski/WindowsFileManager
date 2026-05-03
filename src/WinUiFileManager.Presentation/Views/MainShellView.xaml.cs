using System.Collections.Specialized;
using System.Reactive.Disposables;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Data;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class MainShellView : UserControl
{
    private readonly CompositeDisposable _dataSourceSubscriptions = new();
    private FileEntryTableDataSource? _leftDataSource;
    private FileEntryTableDataSource? _rightDataSource;
    private ObservableCollection<SpecFileEntryViewModel>? _leftItems;
    private ObservableCollection<SpecFileEntryViewModel>? _rightItems;
    private PaneId _activePaneId = PaneId.Left;
    private int _leftSelectedCount;
    private int _rightSelectedCount;
    private bool _fileTablesFrozenForSplitterDrag;

    public MainShellView()
    {
        InitializeComponent();

        LeftEntryTable.Identity = "Left";
        RightEntryTable.Identity = "Right";

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PreviewKeyDown += OnPreviewKeyDown;

        RegisterSplitterHandlers(PaneGridSplitter);
        RegisterSplitterHandlers(InspectorGridSplitter);
        RegisterGlobalPointerReleaseHandlers();

        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
        WeakReferenceMessenger.Default.Register<FileTableSelectionChangedMessage>(this, OnFileTableSelectionChanged);
    }

    public Action? ToggleThemeAction { get; set; }

    public void CapturePaneColumnLayouts()
    {
        // SpecFileEntryTableView owns column layout through messages; persistence will be restored in the next table phase.
    }

    private MainShellViewModel? ViewModel => DataContext as MainShellViewModel;

    public void Initialize(MainShellViewModel viewModel)
    {
        DataContext = viewModel;
        Bindings.Update();
        InspectorView.ViewModel = viewModel.Inspector;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.FocusActivePaneRequested += OnFocusActivePaneRequested;

        UpdateActivePaneBorders();
        UpdateInspectorLayout();
        UpdateStatusBar();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_leftDataSource is not null)
        {
            return;
        }

        var uiScheduler = new DispatcherQueueScheduler(DispatcherQueue);
        _leftDataSource = new FileEntryTableDataSource(
            "Left",
            ResolveInitialPath(
                @"C:\FileEntryTableTest\Left",
                Environment.SpecialFolder.UserProfile),
            uiScheduler);
        _rightDataSource = new FileEntryTableDataSource(
            "Right",
            ResolveInitialPath(
                @"C:\FileEntryTableTest\Right",
                Environment.SpecialFolder.DesktopDirectory),
            uiScheduler);

        _dataSourceSubscriptions.Add(_leftDataSource.States.Subscribe(ApplyLeftState));
        _dataSourceSubscriptions.Add(_rightDataSource.States.Subscribe(ApplyRightState));

        var layout = ColumnLayout.Default;
        WeakReferenceMessenger.Default.Send(new FileTableColumnLayoutMessage("Left", layout));
        WeakReferenceMessenger.Default.Send(new FileTableColumnLayoutMessage("Right", layout));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _dataSourceSubscriptions.Dispose();
        _leftDataSource?.Dispose();
        _rightDataSource?.Dispose();
        _leftDataSource = null;
        _rightDataSource = null;
        SetLeftItems(null);
        SetRightItems(null);
    }

    private void ApplyLeftState(FileEntryTableDataState state)
    {
        SetLeftItems(state.Items);
        LeftEntryTable.ItemsSource = state.Items;
        LeftPathText.Text = state.CurrentPath;
        UpdateStatusBar();
    }

    private void ApplyRightState(FileEntryTableDataState state)
    {
        SetRightItems(state.Items);
        RightEntryTable.ItemsSource = state.Items;
        RightPathText.Text = state.CurrentPath;
        UpdateStatusBar();
    }

    private void SetLeftItems(ObservableCollection<SpecFileEntryViewModel>? items)
    {
        if (ReferenceEquals(_leftItems, items))
        {
            return;
        }

        if (_leftItems is not null)
        {
            _leftItems.CollectionChanged -= OnItemsCollectionChanged;
        }

        _leftItems = items;

        if (_leftItems is not null)
        {
            _leftItems.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void SetRightItems(ObservableCollection<SpecFileEntryViewModel>? items)
    {
        if (ReferenceEquals(_rightItems, items))
        {
            return;
        }

        if (_rightItems is not null)
        {
            _rightItems.CollectionChanged -= OnItemsCollectionChanged;
        }

        _rightItems = items;

        if (_rightItems is not null)
        {
            _rightItems.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateStatusBar();
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (!message.IsFocused)
        {
            return;
        }

        if (message.Identity == "Left")
        {
            ActivatePane(PaneId.Left);
            return;
        }

        if (message.Identity == "Right")
        {
            ActivatePane(PaneId.Right);
        }
    }

    private void OnFileTableSelectionChanged(object recipient, FileTableSelectionChangedMessage message)
    {
        if (message.Identity == "Left")
        {
            _leftSelectedCount = message.SelectedItems.Count;
        }
        else if (message.Identity == "Right")
        {
            _rightSelectedCount = message.SelectedItems.Count;
        }

        UpdateStatusBar();
    }

    private void ActivatePane(PaneId paneId)
    {
        _activePaneId = paneId;

        if (ViewModel is not null)
        {
            var desired = paneId == PaneId.Left ? ViewModel.LeftPane : ViewModel.RightPane;
            if (ViewModel.ActivePane != desired)
            {
                ViewModel.ActivePane = desired;
            }
        }

        UpdateActivePaneBorders();
        UpdateStatusBar();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnMoveClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnCreateFolderClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnFavouritesFlyoutOpening(object sender, object e)
    {
        if (ViewModel is null || sender is not MenuFlyout flyout)
        {
            return;
        }

        while (flyout.Items.Count > 2)
        {
            flyout.Items.RemoveAt(flyout.Items.Count - 1);
        }

        foreach (var fav in ViewModel.Favourites)
        {
            var item = new MenuFlyoutItem
            {
                Text = $"{fav.DisplayName} - {fav.Path.DisplayPath}",
                Tag = fav.Id,
            };
            item.Click += OnFavouriteItemClick;
            flyout.Items.Add(item);
        }

        if (ViewModel.Favourites.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem
            {
                Text = "(no favourites)",
                IsEnabled = false,
            });
        }
    }

    private void OnAddFavouriteClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnFavouriteItemClick(object sender, RoutedEventArgs e)
    {
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
    }

    private void RegisterSplitterHandlers(UIElement splitter)
    {
        splitter.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnSplitterPointerPressed),
            handledEventsToo: true);
    }

    private void RegisterGlobalPointerReleaseHandlers()
    {
        AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnGlobalPointerReleased),
            handledEventsToo: true);
        AddHandler(
            UIElement.PointerCanceledEvent,
            new PointerEventHandler(OnGlobalPointerReleased),
            handledEventsToo: true);
        AddHandler(
            UIElement.PointerCaptureLostEvent,
            new PointerEventHandler(OnGlobalPointerReleased),
            handledEventsToo: true);
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        FreezeFileTablesForSplitterDrag();
    }

    private void OnGlobalPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ReleaseFileTablesAfterSplitterDrag();
    }

    private void FreezeFileTablesForSplitterDrag()
    {
        if (_fileTablesFrozenForSplitterDrag)
        {
            return;
        }

        _fileTablesFrozenForSplitterDrag = true;
    }

    private void ReleaseFileTablesAfterSplitterDrag()
    {
        if (!_fileTablesFrozenForSplitterDrag)
        {
            return;
        }

        if (ViewModel?.IsInspectorVisible == true)
        {
            ViewModel.UpdateInspectorWidthFromLayout(InspectorColumn.ActualWidth);
        }

        _fileTablesFrozenForSplitterDrag = false;
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = IsModifierDown(VirtualKey.Control);
        var shift = IsModifierDown(VirtualKey.Shift);
        var inTextInputContext = IsTextInputFocused();

        switch (e.Key)
        {
            case VirtualKey.Tab when !ctrl:
                ActivatePane(_activePaneId == PaneId.Left ? PaneId.Right : PaneId.Left);
                FocusActiveTable();
                e.Handled = true;
                break;

            case VirtualKey.D when ctrl:
                FavouritesFlyout.ShowAt(FavouritesAppBarButton);
                e.Handled = true;
                break;

            case VirtualKey.I when ctrl:
                ViewModel?.ToggleInspectorCommand.Execute(null);
                e.Handled = true;
                break;

            case VirtualKey.A when ctrl && !shift && !inTextInputContext:
            case VirtualKey.A when ctrl && shift && !inTextInputContext:
            case VirtualKey.L when ctrl:
            case VirtualKey.F2 when !ctrl && !shift && !inTextInputContext:
            case VirtualKey.F6 when shift && !ctrl && !inTextInputContext:
                e.Handled = true;
                break;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainShellViewModel.ActivePane):
                    if (ViewModel is not null)
                    {
                        _activePaneId = ViewModel.ActivePane.PaneId;
                    }

                    UpdateActivePaneBorders();
                    UpdateStatusBar();
                    break;
                case nameof(MainShellViewModel.IsInspectorVisible):
                    Bindings.Update();
                    UpdateInspectorLayout();
                    break;
            }
        });
    }

    private void UpdateActivePaneBorders()
    {
        SetPaneBorder(LeftPaneBorder, _activePaneId == PaneId.Left);
        SetPaneBorder(RightPaneBorder, _activePaneId == PaneId.Right);
    }

    private static void SetPaneBorder(Border border, bool active)
    {
        border.BorderThickness = active ? new Thickness(2) : new Thickness(1);
        border.BorderBrush = active
            ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlForegroundAccentBrush"]
            : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["SystemControlForegroundBaseLowBrush"];
    }

    private void UpdateStatusBar()
    {
        var activeItems = _activePaneId == PaneId.Left ? _leftItems : _rightItems;
        var activeSelectedCount = _activePaneId == PaneId.Left ? _leftSelectedCount : _rightSelectedCount;
        var activePath = _activePaneId == PaneId.Left ? LeftPathText.Text : RightPathText.Text;

        ActivePaneText.Text = _activePaneId == PaneId.Left ? "Left" : "Right";
        PathText.Text = activePath;
        ItemCountText.Text = $"{activeItems?.Count ?? 0} items";
        SelectedText.Text = $"{activeSelectedCount} selected";
    }

    private void OnFocusActivePaneRequested(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(FocusActiveTable);
    }

    private void FocusActiveTable()
    {
        GetActiveTable().Focus(FocusState.Programmatic);
    }

    private TableView GetActiveTable() =>
        _activePaneId == PaneId.Left ? LeftEntryTable.Table : RightEntryTable.Table;

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ToggleThemeAction?.Invoke();
    }

    private void UpdateInspectorLayout()
    {
        if (ViewModel is null)
        {
            return;
        }

        var isVisible = ViewModel.IsInspectorVisible;
        InspectorToggleButton.IsChecked = isVisible;
        InspectorSplitterColumn.Width = isVisible
            ? new GridLength(6, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        InspectorView.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorGridSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsModifierDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private bool IsTextInputFocused()
    {
        var focused = FocusManager.GetFocusedElement(XamlRoot);
        return focused is TextBox
            or PasswordBox
            or RichEditBox
            or AutoSuggestBox;
    }

    private static string ResolveInitialPath(
        string preferredPath,
        Environment.SpecialFolder fallbackFolder)
    {
        if (Directory.Exists(preferredPath))
        {
            return preferredPath;
        }

        var fallbackPath = Environment.GetFolderPath(fallbackFolder);
        if (Directory.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
        {
            return userProfile;
        }

        return Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    }
}
