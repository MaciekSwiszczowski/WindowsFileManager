using System.Collections;
using System.Reflection;
using WinUiFileManager.Presentation.FileEntryTable;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation.MessageLogging;

/// <summary>
/// Diagnostic in-memory log of every app messenger message. On construction it reflects over all loaded
/// assemblies, finds every concrete <see cref="IFileManagerMessengerMessage"/> type, and registers a
/// listener for each, formatting received messages into human-readable lines exposed via
/// <see cref="Entries"/> for the message-log window. Supports pause, clear, and an id substring filter.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> messages arrive on whatever thread sent them, but <see cref="Entries"/> is a
/// UI-bound <see cref="ObservableCollection{T}"/>. The backing <see cref="_allEntries"/> list is guarded
/// by <see cref="_gate"/>, and the UI projection is coalesced onto the captured
/// <see cref="DispatcherQueue"/> via <see cref="ScheduleDisplayedEntriesRefresh"/> (the
/// <see cref="_refreshScheduled"/> flag debounces bursts into a single refresh). The store must be
/// constructed on a dispatcher thread.
/// </para>
/// <para>
/// <b>Lifetime / leak note (AGENTS.md §4):</b> this type registers with the
/// <see cref="StrongReferenceMessenger"/> for many message types but never calls
/// <c>UnregisterAll</c> — it has no <see cref="IDisposable"/>. It is therefore an intentional
/// process-lifetime diagnostic singleton; the messenger rooting it is acceptable only because it lives
/// for the whole process. <see cref="_allEntries"/> is capped at <see cref="MaxEntries"/> to bound memory.
/// </para>
/// </remarks>
public sealed class MessageLogStore
{
    private const int MaxEntries = 500;

    private readonly List<MessageLogEntry> _allEntries = [];
    private readonly Lock _gate = new();
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IMessenger _messenger;
    // Debounce flag: true while a UI refresh is already queued, so bursts coalesce into one refresh.
    private bool _refreshScheduled;
    private string _idFilter = string.Empty;

    /// <exception cref="ArgumentNullException">Thrown when <paramref name="messenger"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when not constructed on a dispatcher thread.</exception>
    public MessageLogStore(IMessenger messenger)
    {
        ArgumentNullException.ThrowIfNull(messenger);
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException($"{nameof(MessageLogStore)} must be created on a dispatcher thread.");
        _messenger = messenger;
        RegisterMessages();
    }

    /// <summary>The filtered, UI-bound log lines (newest first). Mutated only on the dispatcher thread.</summary>
    public ObservableCollection<string> Entries { get; } = [];

    /// <summary>Whether logging is currently paused (incoming messages are dropped while paused).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>Case-insensitive substring filter applied to each entry's id. Setting it trims the value
    /// and schedules a UI refresh.</summary>
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

    /// <summary>Clears all logged entries and refreshes the displayed list.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _allEntries.Clear();
        }

        ScheduleDisplayedEntriesRefresh();
    }

    /// <summary>Toggles whether new messages are recorded.</summary>
    public void TogglePaused() => IsPaused = !IsPaused;

    /// <summary>Discovers every concrete messenger message type and registers a listener for each.</summary>
    private void RegisterMessages()
    {
        foreach (var type in DiscoverConcreteMessengerMessageTypes())
        {
            RegisterListenerForType(type);
        }
    }

    /// <summary>Enumerates all concrete (non-abstract) classes implementing
    /// <see cref="IFileManagerMessengerMessage"/> across loaded assemblies, ordered for stable output.</summary>
    private static IEnumerable<Type> DiscoverConcreteMessengerMessageTypes() =>
        AppDomain.CurrentDomain
            .GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .Where(static t =>
                t is { IsClass: true, IsAbstract: false } &&
                typeof(IFileManagerMessengerMessage).IsAssignableFrom(t))
            .OrderBy(static t => t.FullName, StringComparer.Ordinal);

    /// <summary>Safely lists an assembly's types, degrading gracefully when some types fail to load.</summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some types failed to load; return the ones that did so logging still works.
            return ex.Types.Where(static t => t is not null).Cast<Type>();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Invokes the generic <see cref="RegisterListener{T}"/> for a runtime
    /// <paramref name="messageType"/> via reflection (the messenger Register API is generic).</summary>
    private void RegisterListenerForType(Type messageType)
    {
        var register = typeof(MessageLogStore).GetMethod(
            nameof(RegisterListener),
            BindingFlags.NonPublic | BindingFlags.Instance);
        register!.MakeGenericMethod(messageType).Invoke(this, null);
    }

    // Registers (and intentionally never unregisters — see class remarks) a per-type listener.
    private void RegisterListener<T>()
        where T : class, IFileManagerMessengerMessage =>
        _messenger.Register<T>(this, (_, message) => Append(message));

    /// <summary>Records one received message as a formatted entry (unless paused), trimming the oldest
    /// entries past <see cref="MaxEntries"/>, then schedules a UI refresh. May run on any thread.</summary>
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

    /// <summary>Queues a single UI-thread refresh of <see cref="Entries"/>, debounced so concurrent
    /// appends collapse into one rebuild. Resets the flag if the dispatcher enqueue fails.</summary>
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

    /// <summary>Rebuilds <see cref="Entries"/> from the filtered backing list. Runs on the dispatcher
    /// thread (enqueued by <see cref="ScheduleDisplayedEntriesRefresh"/>).</summary>
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

    /// <summary>Extracts a best-effort id for a message by probing common id-bearing properties
    /// (Identity, SourceIdentity, DialogId) via reflection; empty when none are present.</summary>
    private static string ExtractId<T>(T message)
        where T : class
    {
        var type = message.GetType();
        return ReadPropertyAsString(type, message, "Identity")
            ?? ReadPropertyAsString(type, message, "SourceIdentity")
            ?? ReadPropertyAsString(type, message, "DialogId")
            ?? string.Empty;
    }

    private static string? ReadPropertyAsString(Type type, object instance, string propertyName) =>
        type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance)?.ToString();

    /// <summary>Formats a message's public properties into a readable argument string. Special-cases
    /// <see cref="FileTableSelectionChangedMessage"/> (which would otherwise dump large collections) and
    /// omits the id properties already shown separately.</summary>
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

    /// <summary>Renders a single property value for the log, giving file rows/models their display name
    /// and collections a bracketed, comma-joined form rather than a type name.</summary>
    private static string FormatValue(object? value) =>
        value switch
        {
            null => "null",
            FileListingRow item => SpecFileEntryDisplay.GetName(item.Model),
            FileSystemEntryModel item => item.Name,
            string text => text,
            IEnumerable<FileListingRow> items => $"[{string.Join(", ", items.Select(static item => SpecFileEntryDisplay.GetName(item.Model)))}]",
            IEnumerable<FileSystemEntryModel> items => $"[{string.Join(", ", items.Select(static item => item.Name))}]",
            IEnumerable values => $"[{string.Join(", ", values.Cast<object>())}]",
            _ => value.ToString() ?? string.Empty,
        };

    /// <summary>Formats a selection list as a bracketed, comma-joined list of display names.</summary>
    private static string FormatSelectedItems(IEnumerable<FileListingRow> items) =>
        $"[{string.Join(", ", items.Select(static item => SpecFileEntryDisplay.GetName(item.Model)))}]";
}
