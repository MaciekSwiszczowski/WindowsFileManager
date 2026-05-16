using WinUiFileManager.Presentation.Messaging;

namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

public abstract class FileEntryTableBehaviorBase : Behavior<SpecFileEntryTableView>
{
    private Action<IMessenger>? _messengerSubscribe;
    private IMessenger? _boundMessenger;
    private long _messengerPropertyToken;

    protected FileEntryTableNavigationState? NavigationState { get; private set; }

    protected TableView? EntryTable { get; private set; }

    protected override void OnAttached()
    {
        base.OnAttached();
        NavigationState = AssociatedObject.NavigationState;
    }

    protected override void OnDetaching()
    {
        DetachMessengerObservation();
        StopTrackingTable();
        NavigationState = null;
        base.OnDetaching();
    }

    protected void ObserveMessenger(Action<IMessenger> subscribe)
    {
        _messengerSubscribe = subscribe;
        if (AssociatedObject is null)
        {
            return;
        }

        ApplyMessengerSubscription();
        _messengerPropertyToken = AssociatedObject
            .RegisterPropertyChangedCallback(MessengerProperties.MessengerProperty, OnMessengerPropertyChanged);
    }

    private void OnMessengerPropertyChanged(DependencyObject sender, DependencyProperty dp) => ApplyMessengerSubscription();

    private void ApplyMessengerSubscription()
    {
        ClearMessengerRegistration();
        if (AssociatedObject is null ||
            MessengerProperties.GetMessenger(AssociatedObject) is not { } m ||
            _messengerSubscribe is null)
        {
            return;
        }

        _messengerSubscribe(m);
        _boundMessenger = m;
    }

    private void DetachMessengerObservation()
    {
        if (AssociatedObject is not null && _messengerPropertyToken != 0)
        {
            AssociatedObject.UnregisterPropertyChangedCallback(
                MessengerProperties.MessengerProperty, _messengerPropertyToken);
            _messengerPropertyToken = 0;
        }

        ClearMessengerRegistration();
        _messengerSubscribe = null;
    }

    private void ClearMessengerRegistration()
    {
        if (_boundMessenger is not null)
        {
            _boundMessenger.UnregisterAll(this);
            _boundMessenger = null;
        }
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
