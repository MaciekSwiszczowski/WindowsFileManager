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

    public static readonly DependencyProperty SelectedItemsProperty =
        DependencyProperty.Register(
            nameof(SelectedItems),
            typeof(ObservableCollection<SpecFileEntryViewModel>),
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

    private readonly SpecFileEntryViewModel _parentRow = SpecFileEntryViewModel.CreateParentEntry();
    private ObservableCollection<SpecFileEntryViewModel>? _attachedItemsSource;
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;
    private bool _isObservingKeyboardMessages;
    private bool _syncingSelection;

    public SpecFileEntryTableView()
    {
        InitializeComponent();
        SelectedItems = [];
        WeakReferenceMessenger.Default.Register<FileTableFocusedMessage>(this, OnFileTableFocused);
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

    public ObservableCollection<SpecFileEntryViewModel> SelectedItems
    {
        get => (ObservableCollection<SpecFileEntryViewModel>)GetValue(SelectedItemsProperty);
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

    public ObservableCollection<SpecFileEntryViewModel> ParentRows { get; } = [];

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
            view.RefreshParentRow();
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
            SelectedItems.Clear();
            foreach (var item in EntryTable.SelectedItems.OfType<SpecFileEntryViewModel>())
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
        if (EntryTable.SelectedItem is SpecFileEntryViewModel)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }

    private void Table_Sorting(object sender, TableViewSortingEventArgs e)
    {
        e.Handled = true;
        var column = MapColumn(e.Column.SortMemberPath);
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
    }

    private void FileEntryTableView_GotFocus(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new FileTableFocusedMessage(Identity));
        StartObservingKeyboardMessages();
    }

    private void FileEntryTableView_LostFocus(object sender, RoutedEventArgs e) =>
        StopObservingKeyboardMessages();

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (!StringComparer.Ordinal.Equals(message.Identity, Identity))
        {
            StopObservingKeyboardMessages();
        }
    }

    private void StartObservingKeyboardMessages()
    {
        if (_isObservingKeyboardMessages)
        {
            return;
        }

        _isObservingKeyboardMessages = true;
        WeakReferenceMessenger.Default.Register<MoveCursorUpMessage>(this, (_, _) => MoveCursor(-1));
        WeakReferenceMessenger.Default.Register<MoveCursorDownMessage>(this, (_, _) => MoveCursor(1));
        WeakReferenceMessenger.Default.Register<MoveCursorHomeMessage>(this, (_, _) => MoveToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<MoveCursorEndMessage>(this, (_, _) => MoveToBoundary(moveToEnd: true));
        WeakReferenceMessenger.Default.Register<SelectAllMessage>(this, (_, _) => SelectAllVisibleRows());
        WeakReferenceMessenger.Default.Register<ClearSelectionMessage>(this, (_, _) => ClearSelection());
        WeakReferenceMessenger.Default.Register<ActivateInvokedMessage>(this, (_, _) => ActivateCurrentParentRow());
    }

    private void StopObservingKeyboardMessages()
    {
        if (!_isObservingKeyboardMessages)
        {
            return;
        }

        _isObservingKeyboardMessages = false;
        WeakReferenceMessenger.Default.Unregister<MoveCursorUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorHomeMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorEndMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SelectAllMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ClearSelectionMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateInvokedMessage>(this);
    }

    private void MoveCursor(int delta)
    {
        if (ParentTable.SelectedItem is not null)
        {
            if (delta > 0 && VisibleItems.Count > 0)
            {
                SelectBodyIndex(0);
            }

            return;
        }

        var currentIndex = EntryTable.SelectedItem is SpecFileEntryViewModel current
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
        if (ParentTable.SelectedItem is null || SelectedItems.Count > 0)
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
        var direction = _sortAscending ? SortDirection.Ascending : SortDirection.Descending;
        foreach (var column in ParentTable.Columns)
        {
            column.SortDirection = MapColumn(column.SortMemberPath) == _sortColumn ? direction : null;
        }
    }

    private static FileEntryColumn MapColumn(string? sortMemberPath) =>
        sortMemberPath switch
        {
            nameof(SpecFileEntryViewModel.Name) => FileEntryColumn.Name,
            nameof(SpecFileEntryViewModel.Extension) => FileEntryColumn.Extension,
            nameof(SpecFileEntryViewModel.Size) => FileEntryColumn.Size,
            nameof(SpecFileEntryViewModel.Modified) => FileEntryColumn.Modified,
            nameof(SpecFileEntryViewModel.Attributes) => FileEntryColumn.Attributes,
            _ => FileEntryColumn.Name,
        };
}
