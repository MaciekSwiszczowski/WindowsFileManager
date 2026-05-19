namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableSelectionSnapshotBehavior : FileEntryTableBehaviorBase
{
    private readonly Dictionary<string, SpecFileEntryViewModel> _addedItemsByPath =
        new(StringComparer.OrdinalIgnoreCase);

    private SelectionSnapshot? _snapshot;
    private FileTableApplySelectionSnapshotsRequestedMessage? _pendingApply;
    private ObservableCollection<SpecFileEntryViewModel>? _itemsSource;
    private long _itemsSourcePropertyToken;
    private NormalizedPath? _lastActivePath;

    protected override void OnLoaded(FileEntryTableBehaviorContext context)
    {
        context.Messenger.Register<FileTableCreateSelectionSnapshotsRequestedMessage>(this, OnCreateSnapshotRequested);
        context.Messenger.Register<FileTableApplySelectionSnapshotsRequestedMessage>(this, OnApplySnapshotRequested);
        context.Messenger.Register<FileTableSelectionChangedMessage>(this, OnSelectionChanged);

        AttachItemsSource(context.View.ItemsSource);
        _itemsSourcePropertyToken = context.View.RegisterPropertyChangedCallback(
            SpecFileEntryTableView.ItemsSourceProperty,
            OnItemsSourceChanged);
    }

    protected override void OnUnloaded(FileEntryTableBehaviorContext context)
    {
        context.View.UnregisterPropertyChangedCallback(
            SpecFileEntryTableView.ItemsSourceProperty,
            _itemsSourcePropertyToken);
        AttachItemsSource(null);
        ClearSnapshotState();
        _lastActivePath = null;
    }

    private void OnItemsSourceChanged(DependencyObject sender, DependencyProperty dp)
    {
        AttachItemsSource(Context.View.ItemsSource);
        ClearSnapshotState();
    }

    private void AttachItemsSource(ObservableCollection<SpecFileEntryViewModel>? itemsSource)
    {
        if (ReferenceEquals(_itemsSource, itemsSource))
        {
            return;
        }

        if (_itemsSource is not null)
        {
            _itemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
        }

        _itemsSource = itemsSource;

        if (_itemsSource is not null)
        {
            _itemsSource.CollectionChanged += OnItemsSourceCollectionChanged;
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_snapshot is null)
        {
            return;
        }

        AddReplacementItems(e.NewItems);
        TryApplyPendingSnapshot();
    }

    private void AddReplacementItems(System.Collections.IList? items)
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items.OfType<SpecFileEntryViewModel>())
        {
            if (item.Model is { } model)
            {
                _addedItemsByPath[PathKey(model.FullPath)] = item;
            }
        }
    }

    private void OnSelectionChanged(object _, FileTableSelectionChangedMessage message)
    {
        if (message.Identity != Context.View.Identity)
        {
            return;
        }

        _lastActivePath = message.ActiveItem?.Model?.FullPath;
    }

    private void OnCreateSnapshotRequested(object _, FileTableCreateSelectionSnapshotsRequestedMessage message)
    {
        var context = Context;
        if (!IsCurrentDirectory(context.View, message.DirectoryPath))
        {
            return;
        }

        _snapshot = CreateSnapshot(context, message.DirectoryPath, _lastActivePath);
        _pendingApply = null;
        _addedItemsByPath.Clear();
    }

    private void OnApplySnapshotRequested(object _, FileTableApplySelectionSnapshotsRequestedMessage message)
    {
        var context = Context;
        if (_snapshot is not { } snapshot
            || !IsCurrentDirectory(context.View, message.DirectoryPath)
            || !SameDirectory(snapshot.DirectoryPath, message.DirectoryPath))
        {
            ClearSnapshotState();
            return;
        }

        if (message.OldPath is null || message.NewPath is null)
        {
            ClearSnapshotState();
            return;
        }

        _pendingApply = message;
        TryApplyPendingSnapshot();
    }

    private void TryApplyPendingSnapshot()
    {
        if (_snapshot is not { } snapshot || _pendingApply is not { } pendingApply)
        {
            return;
        }

        if (pendingApply is not { OldPath: { } oldPath, NewPath: { } newPath })
        {
            ClearSnapshotState();
            return;
        }

        if (!CanResolveSelectedItems(snapshot, oldPath, newPath))
        {
            return;
        }

        ApplySnapshot(Context, snapshot, oldPath, newPath);
        ClearSnapshotState();
    }

    private static SelectionSnapshot CreateSnapshot(
        FileEntryTableBehaviorContext context,
        NormalizedPath directoryPath,
        NormalizedPath? lastActivePath)
    {
        var selectedItems = context.Table.SelectedItems
            .OfType<SpecFileEntryViewModel>()
            .Select(static item => item.Model is { } model
                ? new SnapshotItem(model.FullPath, item)
                : null)
            .OfType<SnapshotItem>()
            .ToArray();

        var activeItem = context.NavigationState.GetCurrentItem(context.Table);
        var activeSnapshot = activeItem?.Model is { } activeModel
            ? new SnapshotItem(activeModel.FullPath, activeItem)
            : lastActivePath is { } activePath
                ? new SnapshotItem(activePath, Item: null)
                : null;

        return new SelectionSnapshot(directoryPath, selectedItems, activeSnapshot);
    }

    private bool CanResolveSelectedItems(
        SelectionSnapshot snapshot,
        NormalizedPath oldPath,
        NormalizedPath newPath)
    {
        return snapshot.SelectedItems.All(item => CanResolveItem(item, oldPath, newPath));
    }

    private bool CanResolveItem(
        SnapshotItem item,
        NormalizedPath oldPath,
        NormalizedPath newPath)
    {
        if (!SamePath(item.Path, oldPath))
        {
            return true;
        }

        return _addedItemsByPath.ContainsKey(PathKey(newPath));
    }

    private void ApplySnapshot(
        FileEntryTableBehaviorContext context,
        SelectionSnapshot snapshot,
        NormalizedPath oldPath,
        NormalizedPath newPath)
    {
        var selectedItems = snapshot.SelectedItems
            .Select(item => ResolveItem(item, oldPath, newPath))
            .OfType<SpecFileEntryViewModel>()
            .ToList();

        context.Table.SelectedItems.Clear();
        foreach (var item in selectedItems)
        {
            context.Table.SelectedItems.Add(item);
        }
    }

    private SpecFileEntryViewModel? ResolveItem(
        SnapshotItem item,
        NormalizedPath oldPath,
        NormalizedPath newPath)
    {
        if (!SamePath(item.Path, oldPath))
        {
            return item.Item;
        }

        return _addedItemsByPath.TryGetValue(PathKey(newPath), out var replacementItem)
            ? replacementItem
            : null;
    }

    private void ClearSnapshotState()
    {
        _snapshot = null;
        _pendingApply = null;
        _addedItemsByPath.Clear();
    }

    private static bool IsCurrentDirectory(SpecFileEntryTableView view, NormalizedPath directoryPath) =>
        !string.IsNullOrWhiteSpace(view.CurrentFolder)
        && string.Equals(
            NormalizeDirectoryPath(view.CurrentFolder),
            NormalizeDirectoryPath(directoryPath.DisplayPath),
            StringComparison.OrdinalIgnoreCase);

    private static bool SameDirectory(NormalizedPath left, NormalizedPath right) =>
        string.Equals(
            NormalizeDirectoryPath(left.DisplayPath),
            NormalizeDirectoryPath(right.DisplayPath),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirectoryPath(string path) =>
        path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string PathKey(NormalizedPath path) => path.Value;

    private static bool SamePath(NormalizedPath left, NormalizedPath right) =>
        string.Equals(PathKey(left), PathKey(right), StringComparison.OrdinalIgnoreCase);

    private sealed record SelectionSnapshot(NormalizedPath DirectoryPath, IReadOnlyList<SnapshotItem> SelectedItems, SnapshotItem? ActiveItem);

    private sealed record SnapshotItem(NormalizedPath Path, SpecFileEntryViewModel? Item);
}
