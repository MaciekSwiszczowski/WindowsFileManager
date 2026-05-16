using WinUiFileManager.Application.Messages.RequestMessages;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class CommandButtonsView
{
    private IMessenger? _favouritesMessengerRegistration;
    private bool _isListeningForShortcuts;
    private bool _messageLogButtonInjected;

    public CommandButtonsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public CommandButtonsViewModel ViewModel { get; private set; } = null!;

    public Action? OpenMessageLogWindow { get; set; }

    public void Initialize(CommandButtonsViewModel viewModel, Action? openMessageLogWindow = null)
    {
        OpenMessageLogWindow = openMessageLogWindow;
        _favouritesMessengerRegistration?.UnregisterAll(this);
        _favouritesMessengerRegistration = null;
        _isListeningForShortcuts = false;
        ViewModel = viewModel;
        Bindings.Update();
        TryRegisterFavouritesShortcutListener();
    }

    private void OnLoaded(object _, RoutedEventArgs e)
    {
        TryInjectMessageLogButtonIfConfigured();
        TryRegisterFavouritesShortcutListener();
    }

    private void TryRegisterFavouritesShortcutListener()
    {
        if (_isListeningForShortcuts || !IsLoaded)
        {
            return;
        }

        if (ViewModel?.Messenger is not { } messenger)
        {
            return;
        }

        _isListeningForShortcuts = true;
        _favouritesMessengerRegistration = messenger;
        messenger.Register<OpenFavouritesRequestedMessage>(this, OnOpenFavouritesShortcutRequested);
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

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isListeningForShortcuts = false;
        _favouritesMessengerRegistration?.UnregisterAll(this);
        _favouritesMessengerRegistration = null;
    }

    private void OnFavouritesFlyoutOpening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        while (flyout.Items.Count > 2)
        {
            flyout.Items.RemoveAt(flyout.Items.Count - 1);
        }

        foreach (var favourite in ViewModel.Favourites)
        {
            var item = new MenuFlyoutItem
            {
                Text = $"{favourite.DisplayName} - {favourite.Path.DisplayPath}",
                Tag = favourite.Id,
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

    private void OnOpenFavouritesShortcutRequested(object recipient, OpenFavouritesRequestedMessage message)
    {
        DispatcherQueue.TryEnqueue(() => FavouritesFlyout.ShowAt(FavouritesAppBarButton));
    }
}
