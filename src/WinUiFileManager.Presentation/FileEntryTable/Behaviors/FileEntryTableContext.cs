namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public sealed class FileEntryTableContext
{
    private FileEntryTableContext(
        SpecFileEntryTableView view,
        TableView table,
        IMessenger messenger,
        FileEntryTableNavigationState navigationState)
    {
        View = view;
        Table = table;
        Messenger = messenger;
        NavigationState = navigationState;
    }

    public SpecFileEntryTableView View { get; }

    public TableView Table { get; }

    public IMessenger Messenger { get; }

    public FileEntryTableNavigationState NavigationState { get; }

    public IReadOnlyList<SpecFileEntryViewModel> GetItems() => Table.Items.OfType<SpecFileEntryViewModel>().ToList();

    public IReadOnlyList<SpecFileEntryViewModel> GetSelectedItems() => Table.SelectedItems.OfType<SpecFileEntryViewModel>().ToList();

    public SpecFileEntryViewModel? FindItemByName(string name) =>
        Table.Items
            .OfType<SpecFileEntryViewModel>()
            .FirstOrDefault(item => string.Equals(
                item.Model?.Name,
                name,
                StringComparison.OrdinalIgnoreCase));

    public static FileEntryTableContext Create(SpecFileEntryTableView view)
    {
        if (string.IsNullOrWhiteSpace(view.Identity))
        {
            throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(SpecFileEntryTableView.Identity)} must be set.");
        }

        var table = view.Table
            ?? throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(SpecFileEntryTableView.Table)} must be set.");
        var messenger = view.Messenger
            ?? throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(SpecFileEntryTableView.Messenger)} must be set.");

        return new FileEntryTableContext(view, table, messenger, view.NavigationState);
    }
}
