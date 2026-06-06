using R3;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

using Application.FileEntries;
using Application.Messages.RequestMessages.FileOperations;

/// <summary>
/// Preserves selection and active-row state across a file operation (e.g. a rename) that destroys and
/// re-creates the affected row. On request it snapshots whether the renamed file was selected/active,
/// then watches the items source for the new name to reappear and restores that state on the new row.
/// </summary>
/// <remarks>
/// <para>
/// <b>Known scoping gap:</b> unlike the other pane behaviors, this one registers
/// <see cref="FileTableSelectionSnapshotRequestMessage"/> <i>globally</i> instead of using identity-aware
/// registration — both panes receive every request. It self-filters inside
/// <see cref="OnSnapshotRequested"/> by comparing <see cref="FileTableSelectionSnapshotRequestMessage.DirectoryPath"/>
/// to the view's current folder, which is why it still behaves correctly, but this is inconsistent with
/// the pane-scoping convention. Documented here intentionally; not changed.
/// </para>
/// <para>
/// The watch is an R3 subscription stored in <see cref="_snapshotSubscription"/>; it self-terminates
/// after <see cref="SnapshotTimeout"/> via <c>TakeUntil</c> and is also disposed in
/// <see cref="OnUnloaded"/> so it never outlives the behavior.
/// </para>
/// </remarks>
public sealed class FileEntryTableSelectionSnapshotBehavior : FileEntryTableBehaviorBase
{
    // Upper bound on how long we wait for the renamed entry to reappear before giving up.
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromSeconds(30);
    private IDisposable? _snapshotSubscription;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        // NOTE: registered globally — see the class remarks. Self-filtered by folder.
        context.Messenger.Register<FileTableSelectionSnapshotRequestMessage>(this, OnSnapshotRequested);
    }

    // Dispose the R3 watch on detach; messenger unregistration is handled by the base class.
    protected override void OnUnloaded(FileEntryTableContext context) => ClearSnapshotSubscription();

    private void OnSnapshotRequested(object recipient, FileTableSelectionSnapshotRequestMessage message)
    {
        // Self-filter: ignore requests for a folder this pane is not currently showing.
        if (message.DirectoryPath != Context.View.CurrentFolder ||
            Context.View.ItemsSource is not { } itemsSource)
        {
            return;
        }

        var snapshot = CreateSnapshot(Context, message);
        if (snapshot is null)
        {
            // Nothing relevant was selected/active; tell the requester there is nothing to restore.
            message.TryReply(response: false);
            return;
        }

        // Replace any in-flight watch before starting a new one so we never hold two subscriptions.
        ClearSnapshotSubscription();
        _snapshotSubscription = Observable
            .FromEvent<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                static handler => (_, args) => handler(args),
                handler => itemsSource.CollectionChanged += handler,
                handler => itemsSource.CollectionChanged -= handler)
            .Where(
                (Context.View, Snapshot: snapshot),
                static (_, state) => state.Snapshot.DirectoryPath == state.View.CurrentFolder)
            .TakeUntil(Observable.Timer(SnapshotTimeout))
            .SelectMany(args => FindChangedItems(args.NewItems, snapshot.NewName).ToObservable())
            .Subscribe(
                changedItem => RestoreChangedItem(snapshot, changedItem),
                _ => ClearSnapshotSubscription());

        message.TryReply(response: true);
    }

    private static SelectionSnapshot? CreateSnapshot(FileEntryTableContext context, FileTableSelectionSnapshotRequestMessage message)
    {
        var oldName = GetFileName(message.OldPath);
        var isSelected = IsFileSelected(context, oldName);
        var isActive = IsFileActive(context, oldName);
        return isSelected || isActive
            ? new SelectionSnapshot(message.DirectoryPath, GetFileName(message.NewPath), isSelected, isActive)
            : null;
    }

    private static bool IsFileSelected(FileEntryTableContext context, string name)
    {
        // The synthetic ".." row is navigation UI, not a filesystem entry. It cannot be renamed or updated,
        // so file-operation snapshots only inspect real file rows.
        return context
            .GetSelectedItems()
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .Any(item => HasEqualFileName(item, name));
    }

    private static bool IsFileActive(FileEntryTableContext context, string name)
    {
        var activeItem = context.NavigationState.GetCurrentItem(context.Table)
            ?? context.Table.SelectedItem as SpecFileEntryViewModel;

        // The parent row can be active for navigation, but file operations never replace it.
        return activeItem is not null
            && !SpecFileEntryViewModel.IsParentEntry(activeItem)
            && HasEqualFileName(activeItem, name);
    }

    private void RestoreChangedItem(SelectionSnapshot snapshot, SpecFileEntryViewModel changedItem)
    {
        if (snapshot.WasSelected)
        {
            Context.Table.SelectedItems.Add(changedItem);
        }

        if (snapshot.WasActive)
        {
            RestoreActiveItem(changedItem);
        }

        ClearSnapshotSubscription();
    }

    private void RestoreActiveItem(SpecFileEntryViewModel item)
    {
        Context.Table.SelectedItem = item;
        if (Context.Table.GetRowIndex(item) is not { } idx)
        {
            return;
        }
        // SelectedItem is WinUI state; NavigationState is the table behavior state used by keyboard range
        // selection and active-row navigation, so both need to point at the restored active item.
        Context.NavigationState.SetCurrent(Context.Table, idx, resetSelectionAnchor: true);
        TableViewKeyboardAnchorSynchronizer.Sync(Context.Table, idx);
    }

    /// <summary>Disposes and clears the active items-source watch; safe to call repeatedly.</summary>
    private void ClearSnapshotSubscription()
    {
        _snapshotSubscription?.Dispose();
        _snapshotSubscription = null;
    }

    private static IEnumerable<SpecFileEntryViewModel> FindChangedItems(System.Collections.IList? items, string name) =>
        items is null
            ? []
            : items.OfType<SpecFileEntryViewModel>().Where(item => HasEqualFileName(item, name)).Take(1);

    private static bool HasEqualFileName(SpecFileEntryViewModel item, string name) =>
        item.Model is { } model && string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase);

    private static string GetFileName(NormalizedPath path) => Path.GetFileName(path.DisplayPath);

    /// <summary>Captured selection/active state for a single renamed entry, plus the new file name to
    /// watch for so the state can be re-applied to the recreated row.</summary>
    private sealed record SelectionSnapshot(NormalizedPath DirectoryPath, string NewName, bool WasSelected, bool WasActive);
}
