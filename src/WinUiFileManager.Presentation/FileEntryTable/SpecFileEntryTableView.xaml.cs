using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class SpecFileEntryTableView : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<FileEntryViewModel>),
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

    public static readonly DependencyProperty SortColumnProperty =
        DependencyProperty.Register(
            nameof(SortColumn),
            typeof(FileEntryColumn),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(FileEntryColumn.Name, OnSortStateChanged));

    public static readonly DependencyProperty SortAscendingProperty =
        DependencyProperty.Register(
            nameof(SortAscending),
            typeof(bool),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(true, OnSortStateChanged));

    public static readonly DependencyProperty CurrentItemProperty =
        DependencyProperty.Register(
            nameof(CurrentItem),
            typeof(FileEntryViewModel),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(ObservableCollection<FileEntryViewModel>),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(null));

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

    public static readonly DependencyProperty IsFocusedProperty =
        DependencyProperty.Register(
            nameof(IsFocused),
            typeof(bool),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(false));

    private readonly FileEntryViewModel _parentRow = FileEntryViewModel.CreateParentEntry();
    private ObservableCollection<FileEntryViewModel>? _attachedItemsSource;
    private bool _syncingSelection;

    public SpecFileEntryTableView()
    {
        InitializeComponent();
        SelectedItems = [];
        RegisterKeyboardMessages();
    }

    public ObservableCollection<FileEntryViewModel>? ItemsSource
    {
        get => (ObservableCollection<FileEntryViewModel>?)GetValue(ItemsSourceProperty);
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

    public FileEntryColumn SortColumn
    {
        get => (FileEntryColumn)GetValue(SortColumnProperty);
        set => SetValue(SortColumnProperty, value);
    }

    public bool SortAscending
    {
        get => (bool)GetValue(SortAscendingProperty);
        set => SetValue(SortAscendingProperty, value);
    }

    public FileEntryViewModel? CurrentItem
    {
        get => (FileEntryViewModel?)GetValue(CurrentItemProperty);
        set => SetValue(CurrentItemProperty, value);
    }

    public ObservableCollection<FileEntryViewModel> SelectedItems
    {
        get => (ObservableCollection<FileEntryViewModel>)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
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

    public bool IsFocused
    {
        get => (bool)GetValue(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }

    public ObservableCollection<FileEntryViewModel> ParentRows { get; } = [];

    public ObservableCollection<FileEntryViewModel> VisibleItems { get; } = [];

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.AttachItemsSource(e.NewValue as ObservableCollection<FileEntryViewModel>);
        }
    }

    private static void OnIsRootContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.RefreshParentRow();
        }
    }

    private static void OnSortStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpecFileEntryTableView view)
        {
            view.SyncSortIndicators();
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

    private void AttachItemsSource(ObservableCollection<FileEntryViewModel>? itemsSource)
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

    private void RefreshParentRow()
    {
        ParentRows.Clear();
        if (!IsRootContent)
        {
            ParentRows.Add(_parentRow);
        }
    }

    private void RefreshVisibleItems()
    {
        var selectedNames = SelectedItems.Select(static item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        VisibleItems.Clear();

        foreach (var item in ItemsSource ?? [])
        {
            if (string.IsNullOrEmpty(FilterText)
                || item.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            {
                VisibleItems.Add(item);
            }
        }

        SelectedItems.Clear();
        foreach (var item in VisibleItems.Where(item => selectedNames.Contains(item.Name)))
        {
            SelectedItems.Add(item);
        }

        PublishSelectionChanged();
    }

    private void FileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        ParentTable.RowHeight = 32;
        EntryTable.RowHeight = 32;
        RefreshParentRow();
        RefreshVisibleItems();
        ApplyColumnLayout();
        SyncSortIndicators();
    }

    private void ParentTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection || ParentTable.SelectedItem is null)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            EntryTable.SelectedItems.Clear();
            EntryTable.SelectedItem = null;
            CurrentItem = null;
            SelectedItems.Clear();
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChanged();
        ParentTable.Focus(FocusState.Programmatic);
    }

    private void EntryTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            CurrentItem = EntryTable.SelectedItem as FileEntryViewModel
                ?? e.AddedItems.OfType<FileEntryViewModel>().LastOrDefault();
            SelectedItems.Clear();
            foreach (var item in EntryTable.SelectedItems.OfType<FileEntryViewModel>())
            {
                SelectedItems.Add(item);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChanged();
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (EntryTable.SelectedItem is FileEntryViewModel)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }

    private void Table_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        var column = MapColumn(e.Column.SortMemberPath);
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        SyncSortIndicators();
    }

    private void FileEntryTableView_GotFocus(object sender, RoutedEventArgs e)
    {
        if (IsFocused)
        {
            return;
        }

        IsFocused = true;
        WeakReferenceMessenger.Default.Send(new FileTableFocusedMessage(Identity));
    }

    private void FileEntryTableView_LostFocus(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            IsFocused = focused is not null && IsAncestorOf(focused);
        });
    }

    private void RegisterKeyboardMessages()
    {
        WeakReferenceMessenger.Default.Register<MoveCursorUpMessage>(this, (_, _) => MoveCursor(-1));
        WeakReferenceMessenger.Default.Register<MoveCursorDownMessage>(this, (_, _) => MoveCursor(1));
        WeakReferenceMessenger.Default.Register<MoveCursorHomeMessage>(this, (_, _) => MoveToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<MoveCursorEndMessage>(this, (_, _) => MoveToBoundary(moveToEnd: true));
        WeakReferenceMessenger.Default.Register<SelectAllMessage>(this, (_, _) => SelectAllVisibleRows());
        WeakReferenceMessenger.Default.Register<ClearSelectionMessage>(this, (_, _) => ClearSelection());
        WeakReferenceMessenger.Default.Register<ActivateInvokedMessage>(this, (_, _) => ActivateCurrentParentRow());
    }

    private void MoveCursor(int delta)
    {
        if (!IsFocused)
        {
            return;
        }

        if (ParentTable.SelectedItem is not null)
        {
            if (delta > 0 && VisibleItems.Count > 0)
            {
                SelectBodyIndex(0);
            }

            return;
        }

        var currentIndex = EntryTable.SelectedItem is FileEntryViewModel current
            ? VisibleItems.IndexOf(current)
            : -1;
        var nextIndex = currentIndex + delta;

        if (nextIndex < 0 && !IsRootContent)
        {
            SelectParentRow();
            return;
        }

        if (nextIndex >= 0 && nextIndex < VisibleItems.Count)
        {
            SelectBodyIndex(nextIndex);
        }
    }

    private void MoveToBoundary(bool moveToEnd)
    {
        if (!IsFocused)
        {
            return;
        }

        if (moveToEnd)
        {
            if (VisibleItems.Count > 0)
            {
                SelectBodyIndex(VisibleItems.Count - 1);
            }
            else if (!IsRootContent)
            {
                SelectParentRow();
            }

            return;
        }

        if (!IsRootContent)
        {
            SelectParentRow();
        }
        else if (VisibleItems.Count > 0)
        {
            SelectBodyIndex(0);
        }
    }

    private void SelectParentRow()
    {
        if (ParentRows.Count == 0)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            EntryTable.SelectedItems.Clear();
            EntryTable.SelectedItem = null;
            ParentTable.SelectedItem = ParentRows[0];
            CurrentItem = null;
            SelectedItems.Clear();
        }
        finally
        {
            _syncingSelection = false;
        }

        ParentTable.Focus(FocusState.Programmatic);
        PublishSelectionChanged();
    }

    private void SelectBodyIndex(int index)
    {
        if (index < 0 || index >= VisibleItems.Count)
        {
            return;
        }

        var item = VisibleItems[index];
        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            EntryTable.SelectedItems.Clear();
            EntryTable.SelectedItems.Add(item);
            EntryTable.SelectedItem = item;
            CurrentItem = item;
            SelectedItems.Clear();
            SelectedItems.Add(item);
        }
        finally
        {
            _syncingSelection = false;
        }

        EntryTable.ScrollRowIntoView(index);
        EntryTable.Focus(FocusState.Programmatic);
        PublishSelectionChanged();
    }

    private void SelectAllVisibleRows()
    {
        if (!IsFocused)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            EntryTable.SelectedItems.Clear();
            SelectedItems.Clear();
            foreach (var item in VisibleItems)
            {
                EntryTable.SelectedItems.Add(item);
                SelectedItems.Add(item);
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChanged();
    }

    private void ClearSelection()
    {
        if (!IsFocused)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            EntryTable.SelectedItems.Clear();
            EntryTable.SelectedItem = null;
            SelectedItems.Clear();
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChanged();
    }

    private void ActivateCurrentParentRow()
    {
        if (!IsFocused || ParentTable.SelectedItem is null || SelectedItems.Count > 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(Identity));
    }

    private void PublishSelectionChanged() =>
        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(Identity, SelectedItems.ToList()));

    private void ApplyColumnLayout()
    {
        ApplyColumnLayout(ParentTable);
        ApplyColumnLayout(EntryTable);
    }

    private void ApplyColumnLayout(TableView table)
    {
        foreach (var column in table.Columns)
        {
            column.Width = new GridLength(MapWidth(column.SortMemberPath));
        }
    }

    private double MapWidth(string? sortMemberPath) =>
        MapColumn(sortMemberPath) switch
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
        var direction = SortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in ParentTable.Columns)
        {
            column.SortDirection = MapColumn(column.SortMemberPath) == SortColumn ? direction : null;
        }
    }

    private static FileEntryColumn MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(FileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(FileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(FileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(FileEntryViewModel.LastWriteTime) => FileEntryColumn.Modified,
            nameof(FileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => FileEntryColumn.Name,
        };

    private bool IsAncestorOf(DependencyObject descendant)
    {
        var current = descendant;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
