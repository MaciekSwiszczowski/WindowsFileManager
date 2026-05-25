using DynamicData;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

using Application.FileEntries;
using Messages;

public sealed class FileEntryTableSelectionSnapshotBehavior : FileEntryTableBehaviorBase
{
    private SelectionSnapshot? _snapshot;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Messenger.Register<FileTableCreateSelectionSnapshotsRequestedMessage>(this, OnCreateSnapshotRequested);
        context.Messenger.Register<FileTableApplySelectionSnapshotsRequestedMessage>(this, OnApplySnapshotRequested);
    }

    private void OnCreateSnapshotRequested(object recipient, FileTableCreateSelectionSnapshotsRequestedMessage message)
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

        _snapshot = new SelectionSnapshot(message.DirectoryPath, selectedItems, activeSnapshotItem);
    }

    private void OnApplySnapshotRequested(object recipient, FileTableApplySelectionSnapshotsRequestedMessage message)
    {
        if (_snapshot is not { } snapshot
            || snapshot.DirectoryPath != message.DirectoryPath
            || !IsCurrentDirectory(Context.View, message.DirectoryPath))
        {
            _snapshot = null;
            return;
        }

        snapshot = UpdateChangedItem(snapshot, message);

        RestoreSelectedItems(snapshot.SelectedItems);
        RestoreActiveItem(snapshot.ActiveItem);

        _snapshot = null;
    }

    private SelectionSnapshot UpdateChangedItem(
        SelectionSnapshot snapshot,
        FileTableApplySelectionSnapshotsRequestedMessage message)
    {
        if (message.OldPath is not { } oldPath || message.NewPath is not { } newPath)
        {
            return snapshot;
        }

        var oldName = GetFileName(oldPath);
        var newName = GetFileName(newPath);
        if (!ContainsItem(snapshot, oldName))
        {
            return snapshot;
        }

        var changedItem = Context.FindItemByName(newName);
        if (changedItem is null)
        {
            return snapshot;
        }

        var changedSnapshotItem = new SnapshotItem(newName, changedItem);
        var selectedItems = snapshot.SelectedItems
            .Select(item => SameName(item.Name, oldName) ? changedSnapshotItem : item)
            .ToList();
        var activeItem = snapshot.ActiveItem is { } active && SameName(active.Name, oldName)
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

    private static bool ContainsItem(SelectionSnapshot snapshot, string name) =>
        snapshot.SelectedItems.Any(item => SameName(item.Name, name))
        || snapshot.ActiveItem is { } activeItem && SameName(activeItem.Name, name);

    private static string GetFileName(NormalizedPath path) => Path.GetFileName(path.DisplayPath);

    private static bool SameName(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record SelectionSnapshot(
        NormalizedPath DirectoryPath,
        IReadOnlyList<SnapshotItem> SelectedItems,
        SnapshotItem? ActiveItem);

    private sealed record SnapshotItem(string Name, SpecFileEntryViewModel Item);
}
