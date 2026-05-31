using System.Windows.Input;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.Presentation.Views;

/// <summary>
/// Code-behind for the application's main shell: hosts the dual panels, the file inspector, and the
/// command-button bar, owns the <see cref="KeyboardManager"/> that routes global key gestures, and
/// manages inspector show/hide plus splitter-drag layout updates.
/// </summary>
/// <remarks>
/// Event subscription discipline (AGENTS.md §5):
/// <list type="bullet">
/// <item>Subscriptions to the <i>view model</i> (<c>ViewModel.PropertyChanged</c>,
/// <c>ViewModel.Commands.PropertyChanged</c>) and to the child <c>Panels.PaneSplitterPressed</c> are
/// added in <see cref="Initialize"/> and correctly removed in <see cref="OnUnloaded"/>. This matters
/// because the view model can outlive the view, so leaving these attached would root the view (the
/// <c>WindowManager</c> cautionary case in AGENTS.md §5).</item>
/// <item><b>Leak hazard (described, not fixed):</b> the <c>AddHandler</c> calls in the constructor
/// (<see cref="RegisterSplitterHandlers"/> on the inspector splitter and
/// <see cref="RegisterGlobalPointerReleaseHandlers"/> on this element) and the <c>Unloaded += OnUnloaded</c>
/// subscription are never removed. They target handlers on <c>this</c>/child elements, so they are
/// collected together with the view rather than rooting a longer-lived object — but they are
/// unbalanced <c>AddHandler</c> calls with no matching <c>RemoveHandler</c> on unload.</item>
/// <item><b>VM disposal gap:</b> this view detaches its handlers on unload but does not dispose
/// <see cref="ViewModel"/> or the <see cref="_keyboardManager"/>; their lifetime is owned elsewhere
/// (composition root / window). The <see cref="KeyboardManager"/> created here is never disposed by
/// this view.</item>
/// </list>
/// </remarks>
public sealed partial class MainShellView
{
    private KeyboardManager? _keyboardManager;

    // Guards inspector-width persistence so it is only re-read once per splitter drag gesture.
    private bool _fileTablesFrozenForSplitterDrag;
    public MainShellView()
    {
        InitializeComponent();

        // NOTE: none of these subscriptions are reversed on Unloaded (see class remarks). They are
        // self-targeted (handlers live on this view), so they do not root a longer-lived object.
        Unloaded += OnUnloaded;
        RegisterSplitterHandlers(InspectorGridSplitter);
        RegisterGlobalPointerReleaseHandlers();
    }

    /// <summary>Optional callback to toggle the app theme; forwarded to the commands VM in
    /// <see cref="Initialize"/>.</summary>
    public Action? ToggleThemeAction { get; set; }

    /// <summary>The command bound to keyboard gestures, exposed for XAML key bindings; null until
    /// <see cref="Initialize"/> creates the <see cref="KeyboardManager"/>.</summary>
    public ICommand? KeyboardCommand => _keyboardManager?.KeyPressedCommand;

    public void CapturePaneColumnLayouts()
    {
        // SpecFileEntryTableView owns column layout through messages; persistence will be restored in the next table phase.
    }

    /// <summary>The bound view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before <see cref="Initialize"/>.</exception>
    public MainShellViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(MainShellView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    /// <summary>Wires the view to its view model: creates the keyboard manager, initialises the child
    /// panels/inspector/command bar, and subscribes to VM change notifications. Must be called once
    /// after construction. The VM subscriptions here are the ones reversed in <see cref="OnUnloaded"/>.</summary>
    public void Initialize(MainShellViewModel viewModel, Action? openMessageLogWindow = null)
    {
        _keyboardManager = new KeyboardManager(viewModel.Messenger);
        ViewModel = viewModel;

        Panels.Initialize(viewModel.Panels);
        Panels.PaneSplitterPressed += OnPanelSplitterPressed;

        InspectorView.ViewModel = viewModel.Inspector;
        viewModel.Commands.ToggleThemeAction = ToggleThemeAction;
        CommandButtons.Initialize(viewModel.Commands, openMessageLogWindow);

        // VM can outlive the view: these are removed in OnUnloaded to avoid rooting the view.
        viewModel.PropertyChanged += OnMainShellViewModelPropertyChanged;
        viewModel.Commands.PropertyChanged += OnCommandButtonsPropertyChanged;

        UpdateInspectorLayout();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Reverse the longer-lived (VM/child) subscriptions made in Initialize. The constructor's
        // AddHandler/Unloaded subscriptions are intentionally not reversed here (see class remarks).
        Panels.PaneSplitterPressed -= OnPanelSplitterPressed;

        ViewModel.PropertyChanged -= OnMainShellViewModelPropertyChanged;
        ViewModel.Commands.PropertyChanged -= OnCommandButtonsPropertyChanged;
    }

    // Listens for splitter presses with handledEventsToo so the drag is detected even though the
    // splitter handles the pointer event itself. Not removed on unload (see class remarks).
    private void RegisterSplitterHandlers(UIElement splitter)
    {
        splitter.AddHandler(PointerPressedEvent, new PointerEventHandler(OnSplitterPointerPressed), handledEventsToo: true);
    }

    // Catches pointer release/cancel/capture-lost anywhere in the shell so a splitter drag is always
    // ended even if the pointer is released off the splitter. Not removed on unload (see class remarks).
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

    /// <summary>Marks that a splitter drag is in progress so the expensive inspector-width re-read is
    /// deferred until the drag ends (avoids reflowing the file tables on every pointer move).</summary>
    private void FreezeFileTablesForSplitterDrag()
    {
        if (_fileTablesFrozenForSplitterDrag)
        {
            return;
        }

        _fileTablesFrozenForSplitterDrag = true;
    }

    /// <summary>Ends a splitter drag: persists the new inspector width (when the inspector is visible)
    /// and clears the frozen flag. No-ops if no drag was in progress.</summary>
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

    // VM property changes can arrive off the UI thread; marshal layout updates onto the dispatcher.
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

    /// <summary>Shows/hides the inspector pane, its splitter, and the splitter column according to the
    /// VM's visibility flag. Must run on the UI thread.</summary>
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
