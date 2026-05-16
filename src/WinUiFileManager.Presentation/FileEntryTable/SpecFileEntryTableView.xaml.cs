using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class SpecFileEntryTableView
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<SpecFileEntryViewModel>),
            typeof(SpecFileEntryTableView), new PropertyMetadata(null));

    public static readonly DependencyProperty MessengerProperty =
        DependencyProperty.Register(
            nameof(Messenger),
            typeof(IMessenger),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(null));

    public SpecFileEntryTableView()
    {
        InitializeComponent();
        Loaded += SpecFileEntryTableView_Loaded;
        AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(EntryTable_DoubleTapped), handledEventsToo: true);
    }

    public ObservableCollection<SpecFileEntryViewModel>? ItemsSource
    {
        get => (ObservableCollection<SpecFileEntryViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IMessenger? Messenger
    {
        get => (IMessenger?)GetValue(MessengerProperty);
        set => SetValue(MessengerProperty, value);
    }

    public TableView Table => EntryTable;

    public FileEntryTableNavigationState NavigationState { get; } = new();

    public string Identity { get; set; } = string.Empty;

    private void SpecFileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Identity))
        {
            throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Identity)} must be set.");
        }

        _ = GetRequiredMessenger();
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindItem() is not { } item)
        {
            return;
        }

        var messenger = GetRequiredMessenger();

        if (SpecFileEntryViewModel.IsParentEntry(item))
        {
            messenger.Send(new FileTableNavigateUpRequestedMessage(Identity));
            e.Handled = true;
            return;
        }

        if (item.Model is { Kind: ItemKind.Directory } model)
        {
            messenger.Send(new FileTableNavigateDownRequestedMessage(Identity, model.Name));
            e.Handled = true;
        }
    }

    private IMessenger GetRequiredMessenger() =>
        Messenger
        ?? throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Messenger)} must be set.");

}
