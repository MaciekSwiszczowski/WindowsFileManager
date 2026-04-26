using System.Collections;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.MessageLogging;

public sealed class MessageLogStore
{
    private const int MaxEntries = 500;

    private readonly List<MessageLogEntry> _allEntries = [];
    private readonly IMessenger _messenger;
    private string _idFilter = string.Empty;

    public MessageLogStore(IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        RegisterMessages();
    }

    public ObservableCollection<string> Entries { get; } = [];

    public bool IsPaused { get; private set; }

    public string IdFilter
    {
        get => _idFilter;
        set
        {
            _idFilter = value.Trim();
            RefreshDisplayedEntries();
        }
    }

    public void Clear()
    {
        _allEntries.Clear();
        Entries.Clear();
    }

    public void TogglePaused()
    {
        IsPaused = !IsPaused;
    }

    private void RegisterMessages()
    {
        Register<ActivateInvokedMessage>();
        Register<ClearSelectionMessage>();
        Register<CopyKeyPressedMessage>();
        Register<CopyPathKeyPressedMessage>();
        Register<CopyPathRequestedMessage>();
        Register<CopyRequestedMessage>();
        Register<CreateFolderKeyPressedMessage>();
        Register<CreateFolderRequestedMessage>();
        Register<DefaultActionRequestedMessage>();
        Register<DeleteKeyPressedMessage>();
        Register<DeleteRequestedMessage>();
        Register<ExtendSelectionDownMessage>();
        Register<ExtendSelectionEndMessage>();
        Register<ExtendSelectionHomeMessage>();
        Register<ExtendSelectionPageDownMessage>();
        Register<ExtendSelectionPageUpMessage>();
        Register<ExtendSelectionUpMessage>();
        Register<FileTableFocusedMessage>();
        Register<FileTableNavigateUpRequestedMessage>();
        Register<FileTableSelectionChangedMessage>();
        Register<MoveCursorDownMessage>();
        Register<MoveCursorEndMessage>();
        Register<MoveCursorHomeMessage>();
        Register<MoveCursorPageDownMessage>();
        Register<MoveCursorPageUpMessage>();
        Register<MoveCursorUpMessage>();
        Register<MoveKeyPressedMessage>();
        Register<MoveRequestedMessage>();
        Register<NavigateUpKeyPressedMessage>();
        Register<NavigateUpRequestedMessage>();
        Register<PropertiesKeyPressedMessage>();
        Register<PropertiesRequestedMessage>();
        Register<RenameKeyPressedMessage>();
        Register<RenameRequestedMessage>();
        Register<SelectAllMessage>();
        Register<ToggleSelectionAtCursorAndAdvanceMessage>();
        Register<ToggleSelectionAtCursorMessage>();
    }

    private void Register<T>()
        where T : class
    {
        _messenger.Register<T>(this, (_, message) => Append(message));
    }

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

        _allEntries.Insert(0, entry);
        while (_allEntries.Count > MaxEntries)
        {
            _allEntries.RemoveAt(_allEntries.Count - 1);
        }

        if (MatchesFilter(entry))
        {
            Entries.Insert(0, entry.Text);
            TrimDisplayedEntries();
        }
    }

    private void RefreshDisplayedEntries()
    {
        Entries.Clear();
        foreach (var entry in _allEntries.Where(MatchesFilter))
        {
            Entries.Add(entry.Text);
        }
    }

    private bool MatchesFilter(MessageLogEntry entry) =>
        string.IsNullOrEmpty(IdFilter)
        || entry.Id.Contains(IdFilter, StringComparison.OrdinalIgnoreCase);

    private void TrimDisplayedEntries()
    {
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }

    private static string ExtractId<T>(T message)
        where T : class
    {
        var type = message.GetType();
        return ReadStringProperty(type, message, "Identity")
            ?? ReadStringProperty(type, message, "SourceIdentity")
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
            SpecFileEntryViewModel item => item.Name,
            string text => text,
            IEnumerable<SpecFileEntryViewModel> items => $"[{string.Join(", ", items.Select(static item => item.Name))}]",
            IEnumerable values => $"[{string.Join(", ", values.Cast<object>())}]",
            _ => value.ToString() ?? string.Empty,
        };

    private static string FormatSelectedItems(IEnumerable<SpecFileEntryViewModel> items) =>
        $"[{string.Join(", ", items.Select(static item => item.Name))}]";
}
