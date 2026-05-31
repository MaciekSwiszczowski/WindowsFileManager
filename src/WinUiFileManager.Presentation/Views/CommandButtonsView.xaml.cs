namespace WinUiFileManager.Presentation.Views;

/// <summary>
/// Code-behind for the command-button bar. Beyond binding to its view model, it conditionally injects an
/// extra "Message log" command-bar button when the host supplies an <see cref="OpenMessageLogWindow"/>
/// callback (the button is not declared in XAML because it is only present in diagnostic configurations).
/// </summary>
/// <remarks>
/// Injection is attempted both from <see cref="Initialize"/> and from <c>Loaded</c> because the
/// <see cref="ContentControl.Content"/> command bar may not be realised yet when <c>Initialize</c> runs;
/// <see cref="_messageLogButtonInjected"/> guards against adding the button twice. The constructor's
/// <c>Loaded += OnLoaded</c> and the injected button's <c>Click +=</c> lambda are not explicitly removed,
/// but both target objects owned by this view (the view itself and a child button), so they are
/// collected with it (AGENTS.md §5).
/// </remarks>
public sealed partial class CommandButtonsView
{
    private bool _messageLogButtonInjected;

    public CommandButtonsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    /// <summary>The bound view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before <see cref="Initialize"/>.</exception>
    public CommandButtonsViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(CommandButtonsView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    /// <summary>Host callback that opens the message-log window. When non-null, the message-log button is
    /// injected into the command bar.</summary>
    public Action? OpenMessageLogWindow { get; set; }

    /// <summary>Binds the view to its VM and attempts to inject the message-log button.</summary>
    public void Initialize(CommandButtonsViewModel viewModel, Action? openMessageLogWindow = null)
    {
        OpenMessageLogWindow = openMessageLogWindow;
        ViewModel = viewModel;
        TryInjectMessageLogButtonIfConfigured();
    }

    // Retry injection once the command bar is realised, in case it was not ready during Initialize.
    private void OnLoaded(object _, RoutedEventArgs e)
    {
        TryInjectMessageLogButtonIfConfigured();
    }

    /// <summary>Adds the diagnostic "Message log" button to the command bar exactly once, and only when
    /// a callback is configured and the content command bar exists.</summary>
    private void TryInjectMessageLogButtonIfConfigured()
    {
        if (_messageLogButtonInjected || OpenMessageLogWindow is null || Content is not CommandBar bar)
        {
            return;
        }

        _messageLogButtonInjected = true;
        var dbg = new AppBarButton
        {
            Icon = new SymbolIcon(Symbol.Message),
            Label = "Msgs",
        };
        ToolTipService.SetToolTip(dbg, "Message log");
        // Lambda not detached; the button is a child of this view and collected with it.
        dbg.Click += (_, _) => OpenMessageLogWindow?.Invoke();
        bar.PrimaryCommands.Add(dbg);
    }

}
