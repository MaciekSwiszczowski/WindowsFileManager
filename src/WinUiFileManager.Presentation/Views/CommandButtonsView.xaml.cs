using WinUiFileManager.Presentation.Messaging;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class CommandButtonsView
{
    private IMessenger? _favouritesMessengerRegistration;
    private bool _isListeningForShortcuts;

#if DEBUG
    private bool _debugMessageLogButtonInjected;
#endif

    public CommandButtonsView()
    {
        InitializeComponent();

        RegisterPropertyChangedCallback(MessengerProperties.MessengerProperty, OnMessengerChanged);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnMessengerChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is not CommandButtonsView view)
        {
            return;
        }

        view._favouritesMessengerRegistration?.UnregisterAll(view);
        view._favouritesMessengerRegistration = null;
        view._isListeningForShortcuts = false;
        view.TryRegisterFavouritesShortcutListener();
    }

    public CommandButtonsViewModel ViewModel { get; private set; } = null!;

    public Action? OpenMessageLogWindow { get; set; }

    public void Initialize(CommandButtonsViewModel viewModel, Action? openMessageLogWindow = null)
    {
        OpenMessageLogWindow = openMessageLogWindow;
        ViewModel = viewModel;
        Bindings.Update();
    }

    private void OnLoaded(object _, RoutedEventArgs e)
    {
#if DEBUG
        TryInjectDebugMessageLogButton();
#endif
        TryRegisterFavouritesShortcutListener();
    }

    private void TryRegisterFavouritesShortcutListener()
    {
        if (_isListeningForShortcuts || !IsLoaded)
        {
            return;
        }

        var messenger = MessengerProperties.GetMessenger(this);
        if (messenger is null)
        {
            return;
        }

        _isListeningForShortcuts = true;
        _favouritesMessengerRegistration = messenger;
        messenger.Register<OpenFavouritesRequestedMessage>(this, OnOpenFavouritesShortcutRequested);
    }

#if DEBUG
    private void TryInjectDebugMessageLogButton()
    {
        if (_debugMessageLogButtonInjected || Content is not CommandBar bar)
        {
            return;
        }

        _debugMessageLogButtonInjected = true;
        var dbg = new AppBarButton
        {
            Icon = new SymbolIcon(Symbol.Message),
            Label = "Msgs",
        };
        ToolTipService.SetToolTip(dbg, "Message log (debug build)");
        dbg.Click += (_, _) => OpenMessageLogWindow?.Invoke();
        bar.PrimaryCommands.Add(dbg);
    }
#endif

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
