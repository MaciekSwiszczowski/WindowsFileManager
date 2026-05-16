namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehaviorBase : Behavior<SpecFileEntryTableView>
{
    private IMessenger? _boundMessenger;

    protected FileEntryTableNavigationState? NavigationState { get; private set; }

    protected TableView? EntryTable { get; private set; }

    protected override void OnAttached()
    {
        base.OnAttached();
        NavigationState = AssociatedObject.NavigationState;
        AssociatedObject.Loaded += OnLoaded;
        EnsureTable();
    }

    protected override void OnDetaching()
    {
        ClearMessengerRegistration();
        StopTrackingTable();
        NavigationState = null;
        base.OnDetaching();
    }

    protected virtual void OnMessengerAvailable(IMessenger messenger)
    {
    }

    protected IMessenger GetRequiredMessenger()
    {
        if (AssociatedObject?.Messenger is { } messenger)
        {
            return messenger;
        }

        throw new InvalidOperationException(
            $"{nameof(SpecFileEntryTableView)}.{nameof(SpecFileEntryTableView.Messenger)} must be set.");
    }

    private void RegisterMessenger()
    {
        if (_boundMessenger is not null)
        {
            return;
        }

        var messenger = GetRequiredMessenger();
        OnMessengerAvailable(messenger);
        _boundMessenger = messenger;
    }

    private void ClearMessengerRegistration()
    {
        if (_boundMessenger is not null)
        {
            _boundMessenger.UnregisterAll(this);
            _boundMessenger = null;
        }
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

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureTable();
        RegisterMessenger();
    }

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
