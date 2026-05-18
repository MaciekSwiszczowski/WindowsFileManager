using System.Windows.Input;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class MainShellView
{
    private KeyboardManager? _keyboardManager;

    private bool _fileTablesFrozenForSplitterDrag;
    public MainShellView()
    {
        InitializeComponent();

        Unloaded += OnUnloaded;
        RegisterSplitterHandlers(InspectorGridSplitter);
        RegisterGlobalPointerReleaseHandlers();
    }

    public Action? ToggleThemeAction { get; set; }

    public ICommand? KeyboardCommand => _keyboardManager?.KeyPressedCommand;

    public void CapturePaneColumnLayouts()
    {
        // SpecFileEntryTableView owns column layout through messages; persistence will be restored in the next table phase.
    }

    public MainShellViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(MainShellView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    public void Initialize(MainShellViewModel viewModel, Action? openMessageLogWindow = null)
    {
        _keyboardManager = new KeyboardManager(viewModel.Messenger);
        ViewModel = viewModel;

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

        ViewModel.PropertyChanged -= OnMainShellViewModelPropertyChanged;
        ViewModel.Commands.PropertyChanged -= OnCommandButtonsPropertyChanged;
    }

    private void RegisterSplitterHandlers(UIElement splitter)
    {
        splitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSplitterPointerPressed), handledEventsToo: true);
    }

    private void RegisterGlobalPointerReleaseHandlers()
    {
        AddHandler(PointerReleasedEvent, new PointerEventHandler(OnGlobalPointerReleased), handledEventsToo: true);
        AddHandler(PointerCanceledEvent, new PointerEventHandler(OnGlobalPointerReleased), handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, new PointerEventHandler(OnGlobalPointerReleased), handledEventsToo: true);
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e) => FreezeFileTablesForSplitterDrag();

    private void OnPanelSplitterPressed(object? sender, EventArgs e) => FreezeFileTablesForSplitterDrag();

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

        if (ViewModel.IsInspectorVisible)
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
                if (ViewModel.ParallelExecutionEnabled != ViewModel.Commands.ParallelExecutionEnabled)
                {
                    ViewModel.ParallelExecutionEnabled = ViewModel.Commands.ParallelExecutionEnabled;
                }
            });
        }
    }

    private void UpdateInspectorLayout()
    {
        var isVisible = ViewModel.IsInspectorVisible;
        InspectorSplitterColumn.Width = isVisible
            ? new GridLength(6, GridUnitType.Pixel)
            : new GridLength(0, GridUnitType.Pixel);
        InspectorView.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        InspectorGridSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
