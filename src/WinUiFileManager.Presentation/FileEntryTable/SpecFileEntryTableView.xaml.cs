using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class SpecFileEntryTableView
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<SpecFileEntryViewModel>),
            typeof(SpecFileEntryTableView), new PropertyMetadata(null));

    public SpecFileEntryTableView()
    {
        InitializeComponent();
        Loaded += SpecFileEntryTableView_Loaded;
        AddHandler(
            UIElement.DoubleTappedEvent,
            new DoubleTappedEventHandler(EntryTable_DoubleTapped),
            handledEventsToo: true);
    }

    public ObservableCollection<SpecFileEntryViewModel>? ItemsSource
    {
        get => (ObservableCollection<SpecFileEntryViewModel>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string Identity { get; set; } = string.Empty;

    private void SpecFileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Identity))
        {
            throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Identity)} must be set.");
        }
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindItem() is not { } item)
        {
            return;
        }

        if (item.Model is null)
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(Identity));
            e.Handled = true;
            return;
        }

        if (item.EntryKind == FileEntryKind.Folder)
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateDownRequestedMessage(Identity, item));
            e.Handled = true;
        }
    }
}
