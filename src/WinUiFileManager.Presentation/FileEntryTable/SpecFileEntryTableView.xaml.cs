using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class SpecFileEntryTableView
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<SpecFileEntryViewModel>),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty IsRootContentProperty =
        DependencyProperty.Register(
            nameof(IsRootContent),
            typeof(bool),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(false, OnIsRootContentChanged));

    public static readonly DependencyProperty ColumnLayoutProperty =
        DependencyProperty.Register(
            nameof(ColumnLayout),
            typeof(ColumnLayout),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(ColumnLayout.Default, OnColumnLayoutChanged));

    private readonly SpecFileEntryViewModel _parentRow = SpecFileEntryViewModel.CreateParentEntry();
    private IComparer<SpecFileEntryViewModel> _itemComparer = SpecFileEntryComparer.Create(FileEntryColumn.Name, ascending: true);
    private ObservableCollection<SpecFileEntryViewModel>? _attachedItemsSource;

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

    public bool IsRootContent
    {
        get => (bool)GetValue(IsRootContentProperty);
        set => SetValue(IsRootContentProperty, value);
    }

    public string Identity { get; set; } = string.Empty;

    public ColumnLayout ColumnLayout
    {
        get => (ColumnLayout)GetValue(ColumnLayoutProperty);
        set => SetValue(ColumnLayoutProperty, value);
    }

    public ObservableCollection<SpecFileEntryViewModel> VisibleItems { get; } = [];

    private void SpecFileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Identity))
        {
            throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Identity)} must be set.");
        }
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.AttachItemsSource(e.NewValue as ObservableCollection<SpecFileEntryViewModel>);
        }
    }

    private static void OnIsRootContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.RefreshVisibleItems();
        }
    }

    private static void OnColumnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.ApplyColumnLayout();
        }
    }

    private void AttachItemsSource(ObservableCollection<SpecFileEntryViewModel>? itemsSource)
    {
        if (_attachedItemsSource is not null)
        {
            _attachedItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
        }

        _attachedItemsSource = itemsSource;

        if (_attachedItemsSource is not null)
        {
            _attachedItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
        }

        RefreshVisibleItems();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RefreshVisibleItems();

    private void RefreshVisibleItems()
    {
        VisibleItems.Clear();

        if (!IsRootContent)
        {
            VisibleItems.Add(_parentRow);
        }

        foreach (var item in (ItemsSource ?? []).Order(_itemComparer))
        {
            VisibleItems.Add(item);
        }
    }

    internal void SetItemComparer(IComparer<SpecFileEntryViewModel> itemComparer)
    {
        _itemComparer = itemComparer;
        RefreshVisibleItems();
    }

    private void FileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        EntryTable.RowHeight = 32;
        RefreshVisibleItems();
        ApplyColumnLayout();
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindItem() is not null)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }

    private void ApplyColumnLayout()
    {
        ApplyColumnLayout(EntryTable);
    }

    private void ApplyColumnLayout(TableView table)
    {
        foreach (var column in table.Columns)
        {
            if (MapColumn(column.SortMemberPath) is { } fileEntryColumn)
            {
                column.Width = new GridLength(MapWidth(fileEntryColumn));
            }
        }
    }

    private double MapWidth(FileEntryColumn column) =>
        column switch
        {
            FileEntryColumn.Name => ColumnLayout.NameWidth,
            FileEntryColumn.Extension => ColumnLayout.ExtensionWidth,
            FileEntryColumn.Size => ColumnLayout.SizeWidth,
            FileEntryColumn.Modified => ColumnLayout.ModifiedWidth,
            FileEntryColumn.Attributes => ColumnLayout.AttributesWidth,
            _ => ColumnLayout.NameWidth,
        };

    private static FileEntryColumn? MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(SpecFileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(SpecFileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(SpecFileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(SpecFileEntryViewModel.Modified) => FileEntryColumn.Modified,
            nameof(SpecFileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => null,
        };
}
