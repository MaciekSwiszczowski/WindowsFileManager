using WinUiFileManager.Presentation.FileEntryTable.Data;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class MainShellView
{
    private bool _fileTablesFrozenForSplitterDrag;

    public MainShellView()
    {
        InitializeComponent();

        Unloaded += OnUnloaded;
        RegisterSplitterHandlers(InspectorGridSplitter);
        RegisterGlobalPointerReleaseHandlers();
    }

    public Action? ToggleThemeAction { get; set; }

    public FileEntryTableDataSourceFactory? DataSourceFactory { get; set; }

    public KeyboardManager KeyboardManager { get; } = new();

    public void CapturePaneColumnLayouts()
    {
        // SpecFileEntryTableView owns column layout through messages; persistence will be restored in the next table phase.
    }

    private MainShellViewModel? ViewModel => DataContext as MainShellViewModel;

    public void Initialize(MainShellViewModel viewModel, Action? openMessageLogWindow = null)
    {
        DataContext = viewModel;
        Bindings.Update();

        Panels.DataSourceFactory = DataSourceFactory;
        Panels.Initialization = viewModel.Initialization;
        Panels.Initialize(viewModel.Panels);
        Panels.PaneSplitterPressed += OnPanelSplitterPressed;

        InspectorView.ViewModel = viewModel.Inspector;
        viewModel.Commands.ToggleThemeAction = ToggleThemeAction;
        CommandButtons.Initialize(viewModel.Commands, openMessageLogWindow);

        viewModel.PropertyChanged += OnMainShellViewModelPropertyChanged;
        viewModel.Commands.PropertyChanged += OnCommandButtonsPropertyChanged;

        UpdateInspectorLayout();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Panels.PaneSplitterPressed -= OnPanelSplitterPressed;

        if (ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.PropertyChanged -= OnMainShellViewModelPropertyChanged;
        viewModel.Commands.PropertyChanged -= OnCommandButtonsPropertyChanged;
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

    private void OnPanelSplitterPressed(object? sender, EventArgs e)
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

    private void OnMainShellViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainShellViewModel.IsInspectorVisible))
        {
            DispatcherQueue.TryEnqueue(UpdateInspectorLayout);
        }
    }

    private void OnCommandButtonsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandButtonsViewModel.ParallelExecutionEnabled))
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel is not null && ViewModel.ParallelExecutionEnabled != ViewModel.Commands.ParallelExecutionEnabled)
                {
                    ViewModel.ParallelExecutionEnabled = ViewModel.Commands.ParallelExecutionEnabled;
                }
            });
        }
    }

    private void UpdateInspectorLayout()
    {
        if (ViewModel is null)
        {
            return;
        }

        var isVisible = ViewModel.IsInspectorVisible;
        InspectorSplitterColumn.Width = isVisible
            ? new GridLength(6, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        InspectorView.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorGridSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
