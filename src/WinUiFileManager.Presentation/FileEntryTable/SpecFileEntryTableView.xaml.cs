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
        if ((e.OriginalSource as DependencyObject).FindItem() is not null)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }
}
