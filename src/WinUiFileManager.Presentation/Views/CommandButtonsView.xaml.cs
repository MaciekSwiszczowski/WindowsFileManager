namespace WinUiFileManager.Presentation.Views;

public sealed partial class CommandButtonsView
{
    private bool _isListeningForShortcuts;

    public CommandButtonsView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public CommandButtonsViewModel ViewModel { get; private set; } = null!;

    public void Initialize(CommandButtonsViewModel viewModel)
    {
        ViewModel = viewModel;
        Bindings.Update();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isListeningForShortcuts)
        {
            return;
        }

        _isListeningForShortcuts = true;
        WeakReferenceMessenger.Default.Register<OpenFavouritesRequestedMessage>(this, OnOpenFavouritesShortcutRequested);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isListeningForShortcuts = false;
        WeakReferenceMessenger.Default.UnregisterAll(this);
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
        // MenuFlyout has no bindable open state; Ctrl+D must open it against its placement target.
        DispatcherQueue.TryEnqueue(() => FavouritesFlyout.ShowAt(FavouritesAppBarButton));
    }
}
