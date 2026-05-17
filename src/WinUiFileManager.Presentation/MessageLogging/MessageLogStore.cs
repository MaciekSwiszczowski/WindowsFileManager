using System.Collections;
using System.Reflection;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Presentation.FileEntryTable;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.MessageLogging;

public sealed class MessageLogStore
{
    private const int MaxEntries = 500;

    private readonly List<MessageLogEntry> _allEntries = [];
    private readonly object _gate = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IMessenger _messenger;
    private bool _refreshScheduled;
    private string _idFilter = string.Empty;

    public MessageLogStore(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException($"{nameof(MessageLogStore)} must be created on a dispatcher thread.");
        _messenger = messenger;
        RegisterMessages();
    }

    public ObservableCollection<string> Entries { get; } = [];

    public bool IsPaused { get; private set; }

    public string IdFilter
    {
        get
        {
            lock (_gate)
            {
                return _idFilter;
            }
        }
        set
        {
            lock (_gate)
            {
                _idFilter = value.Trim();
            }

            ScheduleDisplayedEntriesRefresh();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _allEntries.Clear();
        }

        ScheduleDisplayedEntriesRefresh();
    }

    public void TogglePaused()
    {
        IsPaused = !IsPaused;
    }

    private void RegisterMessages()
    {
        foreach (var type in DiscoverConcreteMessengerMessageTypes())
        {
            RegisterListenerForType(type);
        }
    }

    private static IEnumerable<Type> DiscoverConcreteMessengerMessageTypes() =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(static t =>
                t is { IsClass: true, IsAbstract: false } &&
                typeof(IFileManagerMessengerMessage).IsAssignableFrom(t))
            .OrderBy(static t => t.FullName, StringComparer.Ordinal);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(static t => t is not null).Cast<Type>();
        }
        catch
        {
            return [];
        }
    }

    private void RegisterListenerForType(Type messageType)
    {
        var register = typeof(MessageLogStore).GetMethod(
            nameof(RegisterListener),
            BindingFlags.NonPublic | BindingFlags.Instance);
        register!.MakeGenericMethod(messageType).Invoke(this, null);
    }

    private void RegisterListener<T>()
        where T : class, IFileManagerMessengerMessage =>
        _messenger.Register<T>(this, (_, message) => Append(message));

    private void Append<T>(T message)
        where T : class
    {
        if (IsPaused)
        {
            return;
        }

        var entry = new MessageLogEntry(
            DateTime.Now,
            typeof(T).Name,
            ExtractId(message),
            FormatArguments(message));

        lock (_gate)
        {
            _allEntries.Insert(0, entry);
            while (_allEntries.Count > MaxEntries)
            {
                _allEntries.RemoveAt(_allEntries.Count - 1);
            }
        }

        ScheduleDisplayedEntriesRefresh();
    }

    private void ScheduleDisplayedEntriesRefresh()
    {
        lock (_gate)
        {
            if (_refreshScheduled)
            {
                return;
            }

            _refreshScheduled = true;
        }

        if (_dispatcherQueue.TryEnqueue(RefreshDisplayedEntries))
        {
            return;
        }

        lock (_gate)
        {
            _refreshScheduled = false;
        }
    }

    private void RefreshDisplayedEntries()
    {
        IReadOnlyList<string> displayedEntries;
        lock (_gate)
        {
            _refreshScheduled = false;
            displayedEntries = _allEntries
                .Where(entry => MatchesFilter(entry, _idFilter))
                .Select(static entry => entry.Text)
                .ToArray();
        }

        Entries.Clear();
        foreach (var entry in displayedEntries)
        {
            Entries.Add(entry);
        }
    }

    private static bool MatchesFilter(MessageLogEntry entry, string idFilter) =>
        string.IsNullOrEmpty(idFilter)
        || entry.Id.Contains(idFilter, StringComparison.OrdinalIgnoreCase);

    private static string ExtractId<T>(T message)
        where T : class
    {
        var type = message.GetType();
        return ReadStringProperty(type, message, "Identity")
            ?? ReadStringProperty(type, message, "SourceIdentity")
            ?? ReadStringProperty(type, message, "DialogId")
            ?? string.Empty;
    }

    private static string? ReadStringProperty(Type type, object instance, string propertyName) =>
        type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance) as string;

    private static string FormatArguments<T>(T message)
        where T : class
    {
        if (message is FileTableSelectionChangedMessage selectionChanged)
        {
            return $"SelectedItems={FormatSelectedItems(selectionChanged.SelectedItems)}, IsParentRowSelected={selectionChanged.IsParentRowSelected}";
        }

        var parts = message
            .GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.Name is not ("Identity" or "SourceIdentity"))
            .Select(property => $"{property.Name}={FormatValue(property.GetValue(message))}");

        return string.Join(", ", parts);
    }

    private static string FormatValue(object? value) =>
        value switch
        {
            null => "null",
            SpecFileEntryViewModel item => SpecFileEntryDisplay.GetName(item.Model),
            FileSystemEntryModel item => item.Name,
            string text => text,
            IEnumerable<SpecFileEntryViewModel> items => $"[{string.Join(", ", items.Select(static item => SpecFileEntryDisplay.GetName(item.Model)))}]",
            IEnumerable<FileSystemEntryModel> items => $"[{string.Join(", ", items.Select(static item => item.Name))}]",
            IEnumerable values => $"[{string.Join(", ", values.Cast<object>())}]",
            _ => value.ToString() ?? string.Empty,
        };

    private static string FormatSelectedItems(IEnumerable<SpecFileEntryViewModel> items) =>
        $"[{string.Join(", ", items.Select(static item => SpecFileEntryDisplay.GetName(item.Model)))}]";
}
