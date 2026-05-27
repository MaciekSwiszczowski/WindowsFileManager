using System.Reactive.Linq;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

using Application.FileEntries;
using Application.Messages.RequestMessages.FileOperations;

public sealed class FileEntryTableSelectionSnapshotBehavior : FileEntryTableBehaviorBase
{
    private static readonly TimeSpan SnapshotTimeout = TimeSpan.FromSeconds(30);
    private IDisposable? _snapshotSubscription;

    protected override void OnLoaded(FileEntryTableContext context)
    {
        context.Messenger.Register<FileTableSelectionSnapshotRequestMessage>(this, OnSnapshotRequested);
    }

    protected override void OnUnloaded(FileEntryTableContext context) => ClearSnapshotSubscription();

    private void OnSnapshotRequested(object recipient, FileTableSelectionSnapshotRequestMessage message)
    {
        if (message.DirectoryPath != Context.View.CurrentFolder ||
            Context.View.ItemsSource is not { } itemsSource)
        {
            return;
        }

        var snapshot = CreateSnapshot(Context, message);
        if (snapshot is null)
        {
            message.TryReply(response: false);
            return;
        }

        ClearSnapshotSubscription();
        _snapshotSubscription = Observable
            .FromEventPattern<NotifyCollectionChangedEventArgs>(itemsSource, nameof(INotifyCollectionChanged.CollectionChanged))
            .Where(_ => snapshot.DirectoryPath == Context.View.CurrentFolder)
            .TakeUntil(Observable.Timer(SnapshotTimeout))
            .SelectMany(args => FindChangedItems(args.EventArgs.NewItems, snapshot.NewName))
            .Subscribe(
                onNext: changedItem => RestoreChangedItem(snapshot, changedItem),
                onCompleted: ClearSnapshotSubscription);

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
        if (Context.Table.GetRowIndex(item) is { } idx)
        {
            // SelectedItem is WinUI state; NavigationState is the table behavior state used by keyboard range
            // selection and active-row navigation, so both need to point at the restored active item.
            Context.NavigationState.SetCurrent(Context.Table, idx, resetSelectionAnchor: true);
        }
    }

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

    private sealed record SelectionSnapshot(NormalizedPath DirectoryPath, string NewName, bool WasSelected, bool WasActive);
}
