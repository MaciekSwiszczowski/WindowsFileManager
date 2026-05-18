namespace WinUiFileManager.Presentation.Views;

public sealed partial class CommandButtonsView
{
    private bool _messageLogButtonInjected;

    public CommandButtonsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    public CommandButtonsViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(CommandButtonsView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    public Action? OpenMessageLogWindow { get; set; }

    public void Initialize(CommandButtonsViewModel viewModel, Action? openMessageLogWindow = null)
    {
        OpenMessageLogWindow = openMessageLogWindow;
        ViewModel = viewModel;
    }

    private void OnLoaded(object _, RoutedEventArgs e)
    {
        TryInjectMessageLogButtonIfConfigured();
    }

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
        dbg.Click += (_, _) => OpenMessageLogWindow?.Invoke();
        bar.PrimaryCommands.Add(dbg);
    }

}
