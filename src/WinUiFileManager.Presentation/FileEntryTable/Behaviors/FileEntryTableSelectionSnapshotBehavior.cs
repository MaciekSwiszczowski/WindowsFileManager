using DynamicData;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

using Application.FileEntries;
using Application.Messages.RequestMessages.FileOperations;

public sealed class FileEntryTableSelectionSnapshotBehavior : FileEntryTableBehaviorBase
{
    private SelectionSnapshot? _snapshot;
    private ObservableCollection<SpecFileEntryViewModel>? _itemsSource;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Messenger.Register<FileTableSelectionSnapshotRequestMessage>(this, OnSnapshotRequested);
    }

    protected override void OnUnloaded(FileEntryTableContext context) => ClearSnapshot();

    private void OnSnapshotRequested(object recipient, FileTableSelectionSnapshotRequestMessage message)
    {
        var context = Context;
        if (!IsCurrentDirectory(context.View, message.DirectoryPath))
        {
            return;
        }

        var selectedItems = context.GetSelectedItems()
            .Where(static item => item.Model is not null)
            .Select(static item => new SnapshotItem(item.Model!.Name, item))
            .ToList();

        var activeItem = context.NavigationState.GetCurrentItem(context.Table)
            ?? context.Table.SelectedItem as SpecFileEntryViewModel;
        var activeSnapshotItem = activeItem?.Model is { } activeModel
            ? new SnapshotItem(activeModel.Name, activeItem)
            : null;

        var oldName = GetFileName(message.OldPath);
        if (!ContainsItem(selectedItems, activeSnapshotItem, oldName)
            || context.View.ItemsSource is not { } itemsSource)
        {
            Reply(message, false);
            return;
        }

        ClearSnapshot();
        _snapshot = new SelectionSnapshot(message.DirectoryPath, selectedItems, activeSnapshotItem, oldName, GetFileName(message.NewPath));
        _itemsSource = itemsSource;
        _itemsSource.CollectionChanged += OnItemsSourceCollectionChanged;

        Reply(message, true);
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_snapshot is not { } snapshot
            || !IsCurrentDirectory(Context.View, snapshot.DirectoryPath)
            || FindChangedItem(e.NewItems, snapshot.NewName) is not { } changedItem)
        {
            return;
        }

        snapshot = UpdateChangedItem(snapshot, changedItem);
        RestoreSelectedItems(snapshot.SelectedItems);
        RestoreActiveItem(snapshot.ActiveItem);
        ClearSnapshot();
    }

    private SelectionSnapshot UpdateChangedItem(SelectionSnapshot snapshot, SpecFileEntryViewModel changedItem)
    {
        var changedSnapshotItem = new SnapshotItem(snapshot.NewName, changedItem);
        var selectedItems = snapshot.SelectedItems
            .Select(item => SameName(item.Name, snapshot.OldName) ? changedSnapshotItem : item)
            .ToList();
        var activeItem = snapshot.ActiveItem is { } active && SameName(active.Name, snapshot.OldName)
            ? changedSnapshotItem
            : snapshot.ActiveItem;

        return snapshot with
        {
            SelectedItems = selectedItems,
            ActiveItem = activeItem,
        };
    }

    private void RestoreSelectedItems(IEnumerable<SnapshotItem> selectedItems)
    {
        Context.Table.SelectedItems.Clear();
        Context.Table.SelectedItems.AddRange(selectedItems.Select(static item => item.Item));
    }

    private void RestoreActiveItem(SnapshotItem? activeItem)
    {
        if (activeItem is null)
        {
            return;
        }

        Context.Table.SelectedItem = activeItem.Item;
        if (Context.Table.GetRowIndex(activeItem.Item) is { } idx)
        {
            // SelectedItem is WinUI state; NavigationState is the table behavior state used by keyboard range
            // selection and active-row navigation, so both need to point at the restored active item.
            Context.NavigationState.SetCurrent(Context.Table, idx, resetSelectionAnchor: true);
        }
    }

    private static bool IsCurrentDirectory(SpecFileEntryTableView view, NormalizedPath directoryPath) =>
        !string.IsNullOrWhiteSpace(view.CurrentFolder)
        && directoryPath == view.CurrentFolder;

    private void ClearSnapshot()
    {
        if (_itemsSource is not null)
        {
            _itemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
            _itemsSource = null;
        }

        _snapshot = null;
    }

    private static SpecFileEntryViewModel? FindChangedItem(System.Collections.IList? items, string name) =>
        items?
            .OfType<SpecFileEntryViewModel>()
            .FirstOrDefault(item => SameName(item.Model?.Name, name));

    private static bool ContainsItem(
        IReadOnlyList<SnapshotItem> selectedItems,
        SnapshotItem? activeItem,
        string name) =>
        selectedItems.Any(item => SameName(item.Name, name))
        || activeItem is not null && SameName(activeItem.Name, name);

    private static string GetFileName(NormalizedPath path) => Path.GetFileName(path.DisplayPath);

    private static bool SameName(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void Reply(FileTableSelectionSnapshotRequestMessage message, bool response)
    {
        if (!message.HasReceivedResponse)
        {
            message.Reply(response);
        }
    }

    private sealed record SelectionSnapshot(
        NormalizedPath DirectoryPath,
        IReadOnlyList<SnapshotItem> SelectedItems,
        SnapshotItem? ActiveItem,
        string OldName,
        string NewName);

    private sealed record SnapshotItem(string Name, SpecFileEntryViewModel Item);
}
