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

    public static readonly DependencyProperty IdentityProperty =
        DependencyProperty.Register(
            nameof(Identity),
            typeof(string),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FilterTextProperty =
        DependencyProperty.Register(
            nameof(FilterText),
            typeof(string),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(string.Empty, OnFilterTextChanged));

    public static readonly DependencyProperty ColumnLayoutProperty =
        DependencyProperty.Register(
            nameof(ColumnLayout),
            typeof(ColumnLayout),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(ColumnLayout.Default, OnColumnLayoutChanged));

    private readonly SpecFileEntryViewModel _parentRow = SpecFileEntryViewModel.CreateParentEntry();
    private ObservableCollection<SpecFileEntryViewModel>? _attachedItemsSource;
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;

    public SpecFileEntryTableView()
    {
        InitializeComponent();
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

    public string Identity
    {
        get => (string)GetValue(IdentityProperty);
        set => SetValue(IdentityProperty, value);
    }

    public string FilterText
    {
        get => (string)GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    public ColumnLayout ColumnLayout
    {
        get => (ColumnLayout)GetValue(ColumnLayoutProperty);
        set => SetValue(ColumnLayoutProperty, value);
    }

    public ObservableCollection<SpecFileEntryViewModel> VisibleItems { get; } = [];

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

    private static void OnFilterTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

        foreach (var item in GetFilteredItems().Order(SpecFileEntryComparer.Create(_sortColumn, _sortAscending)))
        {
            VisibleItems.Add(item);
        }
    }

    private IEnumerable<SpecFileEntryViewModel> GetFilteredItems()
    {
        foreach (var item in ItemsSource ?? [])
        {
            if (string.IsNullOrEmpty(FilterText)
                || item.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            {
                yield return item;
            }
        }
    }

    private void FileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        EntryTable.RowHeight = 32;
        RefreshVisibleItems();
        ApplyColumnLayout();
        SyncSortIndicators();
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindItem() is not null)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }

    private void Table_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        if (MapColumn(e.Column.SortMemberPath) is not { } column)
        {
            return;
        }

        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        SyncSortIndicators();
        RefreshVisibleItems();
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

    private void SyncSortIndicators()
    {
        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in EntryTable.Columns)
        {
            column.SortDirection = MapColumn(column.SortMemberPath) == _sortColumn ? direction : null;
        }
    }

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

    private sealed class SpecFileEntryComparer : IComparer<SpecFileEntryViewModel>
    {
        private static readonly StringComparer TextComparer = StringComparer.CurrentCultureIgnoreCase;

        private readonly FileEntryColumn _column;
        private readonly bool _ascending;

        private SpecFileEntryComparer(FileEntryColumn column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        public static SpecFileEntryComparer Create(FileEntryColumn column, bool ascending) =>
            new(column, ascending);

        public int Compare(SpecFileEntryViewModel? x, SpecFileEntryViewModel? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return _ascending ? -1 : 1;
            }

            if (y is null)
            {
                return _ascending ? 1 : -1;
            }

            var result = CompareByColumn(x, y);
            if (result == 0 && _column != FileEntryColumn.Name)
            {
                result = TextComparer.Compare(x.Name, y.Name);
            }

            return _ascending ? result : -result;
        }

        private int CompareByColumn(SpecFileEntryViewModel x, SpecFileEntryViewModel y) =>
            _column switch
            {
                FileEntryColumn.Name => TextComparer.Compare(x.Name, y.Name),
                FileEntryColumn.Extension => TextComparer.Compare(x.Extension, y.Extension),
                FileEntryColumn.Size => Nullable.Compare(x.Model?.Size, y.Model?.Size),
                FileEntryColumn.Modified => x.Modified.CompareTo(y.Modified),
                FileEntryColumn.Attributes => TextComparer.Compare(x.Attributes, y.Attributes),
                _ => TextComparer.Compare(x.Name, y.Name),
            };
    }
}
