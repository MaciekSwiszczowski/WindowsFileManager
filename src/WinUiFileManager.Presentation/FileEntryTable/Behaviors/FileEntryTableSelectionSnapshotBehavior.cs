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
        if (!IsCurrentDirectory(Context.View, message.DirectoryPath) ||
            Context.View.ItemsSource is not { } itemsSource)
        {
            return;
        }

        var snapshot = CreateSnapshot(Context, message);
        if (snapshot is null)
        {
            Reply(message, response: false);
            return;
        }

        ClearSnapshot();
        _snapshot = snapshot;
        ObserveItemsSource(itemsSource);

        Reply(message, response: true);
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

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_snapshot is not { } snapshot
            || !IsCurrentDirectory(Context.View, snapshot.DirectoryPath)
            || FindChangedItem(e.NewItems, snapshot.NewName) is not { } changedItem)
        {
            return;
        }

        RestoreChangedItem(snapshot, changedItem);
        ClearSnapshot();
    }

    private static bool IsFileSelected(FileEntryTableContext context, string name)
    {
        // The synthetic ".." row is navigation UI, not a filesystem entry. It cannot be renamed or updated,
        // so file-operation snapshots only inspect real file rows.
        return context
            .GetSelectedItems()
            .Where(static item => !SpecFileEntryViewModel.IsParentEntry(item))
            .Any(item => HasFileName(item, name));
    }

    private static bool IsFileActive(FileEntryTableContext context, string name)
    {
        var activeItem = context.NavigationState.GetCurrentItem(context.Table)
            ?? context.Table.SelectedItem as SpecFileEntryViewModel;

        // The parent row can be active for navigation, but file operations never replace it.
        return activeItem is not null
            && !SpecFileEntryViewModel.IsParentEntry(activeItem)
            && HasFileName(activeItem, name);
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

    private static bool IsCurrentDirectory(SpecFileEntryTableView view, NormalizedPath directoryPath) =>
        !string.IsNullOrWhiteSpace(view.CurrentFolder)
        && directoryPath == view.CurrentFolder;

    private void ObserveItemsSource(ObservableCollection<SpecFileEntryViewModel> itemsSource)
    {
        _itemsSource = itemsSource;
        _itemsSource.CollectionChanged += OnItemsSourceCollectionChanged;
    }

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
            .FirstOrDefault(item => !SpecFileEntryViewModel.IsParentEntry(item) && HasFileName(item, name));

    private static bool HasFileName(SpecFileEntryViewModel item, string name) =>
        item.Model is { } model && SameName(model.Name, name);

    private static string GetFileName(NormalizedPath path) => Path.GetFileName(path.DisplayPath);

    private static bool SameName(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void Reply(FileTableSelectionSnapshotRequestMessage message, bool response)
    {
        if (message.HasReceivedResponse)
        {
            return;
        }

        message.Reply(response);
    }

    private sealed record SelectionSnapshot(NormalizedPath DirectoryPath, string NewName, bool WasSelected, bool WasActive);
}
