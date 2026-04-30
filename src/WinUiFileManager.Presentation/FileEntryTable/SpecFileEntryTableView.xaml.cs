using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class SpecFileEntryTableView
{
    private const int ParentRowIndex = -1;

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
    private readonly List<SpecFileEntryViewModel> _lastPublishedSelection = [];
    private readonly ObservableCollection<SpecFileEntryViewModel> _selectedItems = [];
    private ObservableCollection<SpecFileEntryViewModel>? _attachedItemsSource;
    private FileEntryColumn _sortColumn = FileEntryColumn.Name;
    private bool _sortAscending = true;
    private bool _isObservingKeyboardMessages;
    private bool _syncingSelection;
    private bool _lastPublishedParentRowSelected;
    private int? _selectionAnchorIndex;
    private int? _selectionCursorIndex;

    public SpecFileEntryTableView()
    {
        InitializeComponent();
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
        var wasParentSelected = IsParentRowSelected;
        ParentRows.Clear();
        if (!IsRootContent)
        {
            ParentRows.Add(_parentRow);
        }
        else if (wasParentSelected)
        {
            ParentTable.SelectedItem = null;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void RefreshVisibleItems()
    {
        var selectedNames = _selectedItems.Select(static item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        VisibleItems.Clear();

        foreach (var item in ItemsSource ?? [])
        {
            if (string.IsNullOrEmpty(FilterText)
                || item.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            {
                VisibleItems.Add(item);
            }
        }

        _selectedItems.Clear();
        foreach (var item in VisibleItems.Where(item => selectedNames.Contains(item.Name)))
        {
            _selectedItems.Add(item);
        }

        PublishSelectionChangedIfNeeded();
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
            _selectedItems.Clear();
            _selectionAnchorIndex = ParentRowIndex;
            _selectionCursorIndex = ParentRowIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
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
            var parentWasSelected = ParentTable.SelectedItem is not null;
            ParentTable.SelectedItem = null;

            SyncSelectedItemsFromEntryTable();
            if (GetBodySelectionIndex(e.AddedItems.OfType<SpecFileEntryViewModel>().LastOrDefault()) is { } index)
            {
                _selectionAnchorIndex = parentWasSelected ? ParentRowIndex : index;
                _selectionCursorIndex = index;
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (EntryTable.SelectedItem is SpecFileEntryViewModel)
        {
            WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
        }
    }

    private void ParentTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ParentTable.SelectedItem is not null && _selectedItems.Count == 0)
        {
            WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(Identity));
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
    }

    private void FileEntryTableView_GotFocus(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new FileTableFocusedMessage(Identity));
        StartObservingKeyboardMessages();
    }

    private void FileEntryTableView_LostFocus(object sender, RoutedEventArgs e)
    {
        StopObservingKeyboardMessages();
    }

    private void OnFileTableFocused(object recipient, FileTableFocusedMessage message)
    {
        if (message.Identity == Identity)
        {
            return;
        }

        StopObservingKeyboardMessages();
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
        WeakReferenceMessenger.Default.Register<MoveCursorPageUpMessage>(this, (_, _) => MoveCursorByPage(-1));
        WeakReferenceMessenger.Default.Register<MoveCursorPageDownMessage>(this, (_, _) => MoveCursorByPage(1));
        WeakReferenceMessenger.Default.Register<MoveCursorHomeMessage>(this, (_, _) => MoveToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<MoveCursorEndMessage>(this, (_, _) => MoveToBoundary(moveToEnd: true));
        WeakReferenceMessenger.Default.Register<ExtendSelectionUpMessage>(this, (_, _) => ExtendSelection(-1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionDownMessage>(this, (_, _) => ExtendSelection(1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionPageUpMessage>(this, (_, _) => ExtendSelectionByPage(-1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionPageDownMessage>(this, (_, _) => ExtendSelectionByPage(1));
        WeakReferenceMessenger.Default.Register<ExtendSelectionHomeMessage>(this, (_, _) => ExtendSelectionToBoundary(moveToEnd: false));
        WeakReferenceMessenger.Default.Register<ExtendSelectionEndMessage>(this, (_, _) => ExtendSelectionToBoundary(moveToEnd: true));
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
        WeakReferenceMessenger.Default.Unregister<MoveCursorPageUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorPageDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorHomeMessage>(this);
        WeakReferenceMessenger.Default.Unregister<MoveCursorEndMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionPageUpMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionPageDownMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionHomeMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ExtendSelectionEndMessage>(this);
        WeakReferenceMessenger.Default.Unregister<SelectAllMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ClearSelectionMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ActivateInvokedMessage>(this);
    }

    private void MoveCursor(int delta)
    {
        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            return;
        }

        var nextIndex = currentIndex.Value + delta;

        if (nextIndex < 0 && HasParentRow)
        {
            SelectParentRow();
            return;
        }

        if (nextIndex >= 0 && nextIndex < VisibleItems.Count)
        {
            SelectBodyIndex(nextIndex);
        }
    }

    private void MoveCursorByPage(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            return;
        }

        var targetIndex = currentIndex.Value + (direction * GetVisibleBodyRowCount());
        if (direction < 0 && targetIndex < 0 && HasParentRow)
        {
            SelectParentRow();
            return;
        }

        if (VisibleItems.Count > 0)
        {
            SelectBodyIndex(Math.Clamp(targetIndex, 0, VisibleItems.Count - 1));
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
            _selectedItems.Clear();
            _selectionAnchorIndex = ParentRowIndex;
            _selectionCursorIndex = ParentRowIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        ParentTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
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
            _selectedItems.Clear();
            _selectedItems.Add(item);
            _selectionAnchorIndex = index;
            _selectionCursorIndex = index;
        }
        finally
        {
            _syncingSelection = false;
        }

        EntryTable.ScrollRowIntoView(index);
        EntryTable.Focus(FocusState.Programmatic);
        PublishSelectionChangedIfNeeded();
    }

    private void SelectAllVisibleRows()
    {
        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            EntryTable.SelectedItems.Clear();
            _selectedItems.Clear();
            foreach (var item in VisibleItems)
            {
                EntryTable.SelectedItems.Add(item);
                _selectedItems.Add(item);
            }

            _selectionAnchorIndex = VisibleItems.Count > 0 ? 0 : null;
            _selectionCursorIndex = VisibleItems.Count > 0 ? VisibleItems.Count - 1 : null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ClearSelection()
    {
        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = null;
            EntryTable.SelectedItems.Clear();
            EntryTable.SelectedItem = null;
            _selectedItems.Clear();
            _selectionAnchorIndex = null;
            _selectionCursorIndex = null;
        }
        finally
        {
            _syncingSelection = false;
        }

        PublishSelectionChangedIfNeeded();
    }

    private void ExtendSelection(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        if (EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        var targetIndex = ClampSelectionIndex(currentIndex + delta);
        ApplySelectionRange(anchor, targetIndex);
    }

    private void ExtendSelectionByPage(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        if (EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var currentIndex = _selectionCursorIndex ?? anchor;
        var targetIndex = ClampSelectionIndex(currentIndex + (direction * GetVisibleBodyRowCount()));
        ApplySelectionRange(anchor, targetIndex);
    }

    private void ExtendSelectionToBoundary(bool moveToEnd)
    {
        if (EnsureSelectionAnchor() is not { } anchor)
        {
            return;
        }

        var targetIndex = moveToEnd
            ? VisibleItems.Count - 1
            : HasParentRow ? ParentRowIndex : 0;

        ApplySelectionRange(anchor, ClampSelectionIndex(targetIndex));
    }

    private int? EnsureSelectionAnchor()
    {
        if (_selectionAnchorIndex is { } anchor)
        {
            return ClampSelectionIndex(anchor);
        }

        var currentIndex = GetCurrentSelectionIndex();
        if (currentIndex is null)
        {
            SelectFirstAvailableRow();
            currentIndex = GetCurrentSelectionIndex();
        }

        _selectionAnchorIndex = currentIndex;
        _selectionCursorIndex = currentIndex;
        return currentIndex;
    }

    private void ApplySelectionRange(int anchorIndex, int cursorIndex)
    {
        if (ParentRows.Count == 0 && VisibleItems.Count == 0)
        {
            return;
        }

        anchorIndex = ClampSelectionIndex(anchorIndex);
        cursorIndex = ClampSelectionIndex(cursorIndex);

        var startIndex = Math.Min(anchorIndex, cursorIndex);
        var endIndex = Math.Max(anchorIndex, cursorIndex);
        var includesParent = HasParentRow && startIndex <= ParentRowIndex && endIndex >= ParentRowIndex;

        _syncingSelection = true;
        try
        {
            ParentTable.SelectedItem = includesParent ? ParentRows[0] : null;
            EntryTable.SelectedItems.Clear();
            _selectedItems.Clear();

            for (var i = Math.Max(0, startIndex); i <= endIndex && i < VisibleItems.Count; i++)
            {
                var item = VisibleItems[i];
                EntryTable.SelectedItems.Add(item);
                _selectedItems.Add(item);
            }

            EntryTable.SelectedItem =
                cursorIndex >= 0 && cursorIndex < VisibleItems.Count
                    ? VisibleItems[cursorIndex]
                    : null;

            _selectionAnchorIndex = anchorIndex;
            _selectionCursorIndex = cursorIndex;
        }
        finally
        {
            _syncingSelection = false;
        }

        if (cursorIndex == ParentRowIndex && includesParent)
        {
            ParentTable.Focus(FocusState.Programmatic);
        }
        else if (cursorIndex >= 0)
        {
            EntryTable.ScrollRowIntoView(cursorIndex);
            EntryTable.Focus(FocusState.Programmatic);
        }

        PublishSelectionChangedIfNeeded();
    }

    private int ClampSelectionIndex(int index)
    {
        var minimumIndex = HasParentRow ? ParentRowIndex : 0;
        var maximumIndex = VisibleItems.Count > 0 ? VisibleItems.Count - 1 : minimumIndex;
        return Math.Clamp(index, minimumIndex, maximumIndex);
    }

    private int? GetCurrentSelectionIndex()
    {
        if (_selectionCursorIndex is { } cursorIndex)
        {
            return ClampSelectionIndex(cursorIndex);
        }

        if (ParentTable.SelectedItem is not null)
        {
            return ParentRowIndex;
        }

        return GetBodySelectionIndex(EntryTable.SelectedItem as SpecFileEntryViewModel)
            ?? GetLastSelectedBodyIndex();
    }

    private int? GetLastSelectedBodyIndex()
    {
        int? lastIndex = null;
        foreach (var item in EntryTable.SelectedItems.OfType<SpecFileEntryViewModel>())
        {
            if (GetBodySelectionIndex(item) is { } index)
            {
                lastIndex = index;
            }
        }

        return lastIndex;
    }

    private int? GetBodySelectionIndex(SpecFileEntryViewModel? item)
    {
        if (item is null)
        {
            return null;
        }

        var index = VisibleItems.IndexOf(item);
        return index >= 0 ? index : null;
    }

    private void SelectFirstAvailableRow()
    {
        if (HasParentRow)
        {
            SelectParentRow();
        }
        else if (VisibleItems.Count > 0)
        {
            SelectBodyIndex(0);
        }
    }

    private int GetVisibleBodyRowCount() =>
        Math.Max(1, (int)(EntryTable.ActualHeight / Math.Max(EntryTable.RowHeight, 1d)) - 1);

    private bool HasParentRow => !IsRootContent && ParentRows.Count > 0;

    private bool IsParentRowSelected => ParentTable.SelectedItem is not null;

    private void ActivateCurrentParentRow()
    {
        if (ParentTable.SelectedItem is null || _selectedItems.Count > 0)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send(new FileTableNavigateUpRequestedMessage(Identity));
    }
    private void SyncSelectedItemsFromEntryTable()
    {
        var selectedItems = EntryTable.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .ToHashSet();

        _selectedItems.Clear();
        foreach (var item in VisibleItems)
        {
            if (selectedItems.Contains(item))
            {
                _selectedItems.Add(item);
            }
        }
    }

    private void PublishSelectionChangedIfNeeded()
    {
        var isParentRowSelected = IsParentRowSelected;
        if (_lastPublishedParentRowSelected == isParentRowSelected
            && _lastPublishedSelection.SequenceEqual(_selectedItems))
        {
            return;
        }

        _lastPublishedParentRowSelected = isParentRowSelected;
        _lastPublishedSelection.Clear();
        _lastPublishedSelection.AddRange(_selectedItems);

        WeakReferenceMessenger.Default.Send(
            new FileTableSelectionChangedMessage(Identity, _selectedItems.ToList(), isParentRowSelected));
    }

    private void ApplyColumnLayout()
    {
        ApplyColumnLayout(ParentTable);
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
        foreach (var column in ParentTable.Columns)
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
}
