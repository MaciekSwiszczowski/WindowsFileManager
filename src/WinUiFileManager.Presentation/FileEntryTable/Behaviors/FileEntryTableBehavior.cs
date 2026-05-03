namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehavior : Behavior<SpecFileEntryTableView>
{
    protected FileEntryTableNavigationState? NavigationState { get; private set; }

    protected TableView? EntryTable { get; private set; }

    protected override void OnAttached()
    {
        base.OnAttached();
        NavigationState = AssociatedObject.NavigationState;
    }

    protected override void OnDetaching()
    {
        StopTrackingTable();
        NavigationState = null;
        base.OnDetaching();
    }

    protected void TrackTableOnLoaded()
    {
        AssociatedObject.Loaded += OnLoaded;
        EnsureTable();
    }

    protected bool EnsureTable()
    {
        if (AssociatedObject is null)
        {
            return false;
        }

        var table = AssociatedObject.Table;
        if (ReferenceEquals(EntryTable, table))
        {
            return true;
        }

        if (EntryTable is not null)
        {
            OnTableDetaching(EntryTable);
        }

        EntryTable = table;
        OnTableAttached(table);
        return true;
    }

    protected virtual void OnTableAttached(TableView table)
    {
    }

    protected virtual void OnTableDetaching(TableView table)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => EnsureTable();

    private void StopTrackingTable()
    {
        if (AssociatedObject is not null)
        {
            AssociatedObject.Loaded -= OnLoaded;
        }

        if (EntryTable is not null)
        {
            OnTableDetaching(EntryTable);
            EntryTable = null;
        }
    }
}
