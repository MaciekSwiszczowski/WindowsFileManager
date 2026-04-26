using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable.Messages;

namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed partial class FileTableMessageLogView : UserControl
{
    public FileTableMessageLogView()
    {
        InitializeComponent();
        Register<FileTableFocusedMessage>();
        Register<FileTableSelectionChangedMessage>();
        Register<FileTableNavigateUpRequestedMessage>();
        Register<ActivateInvokedMessage>();
        Register<MoveCursorUpMessage>();
        Register<MoveCursorDownMessage>();
        Register<MoveCursorHomeMessage>();
        Register<MoveCursorEndMessage>();
        Register<SelectAllMessage>();
        Register<ClearSelectionMessage>();
    }

    public ObservableCollection<string> Entries { get; } = [];

    private void Register<T>()
        where T : class
    {
        WeakReferenceMessenger.Default.Register<T>(this, (_, message) => Append(message));
    }

    private void Append<T>(T message)
        where T : class
    {
        var line = message switch
        {
            FileTableSelectionChangedMessage selection =>
                $"{DateTime.Now:HH:mm:ss.fff} {nameof(FileTableSelectionChangedMessage)} Identity={selection.Identity}, Selected=[{string.Join(", ", selection.SelectedItems.Select(static item => item.Name))}]",
            _ => $"{DateTime.Now:HH:mm:ss.fff} {typeof(T).Name} {message}",
        };

        Entries.Insert(0, line);
        while (Entries.Count > 250)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }
}
