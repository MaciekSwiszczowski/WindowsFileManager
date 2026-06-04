namespace WinUiFileManager.Presentation.FileEntryTable.Behaviors;

/// <summary>
/// Immutable bundle of the moving parts a <see cref="FileEntryTableBehaviorBase"/> needs once its
/// <see cref="SpecFileEntryTableView"/> has loaded: the view, the underlying <see cref="TableView"/>,
/// the pane-scoped <see cref="IFileManagerMessenger"/>, and the shared <see cref="FileEntryTableNavigationState"/>.
/// </summary>
/// <remarks>
/// Created via <see cref="Create"/> only after the view is loaded, because the bound
/// <see cref="SpecFileEntryTableView.Table"/>/<see cref="SpecFileEntryTableView.Messenger"/> and the
/// <see cref="SpecFileEntryTableView.Identity"/> must already be set. The <see cref="Identity"/> on the
/// view is what lets pane-scoped behaviors use identity-aware messenger registrations.
/// </remarks>
public sealed class FileEntryTableContext
{
    private FileEntryTableContext(
        SpecFileEntryTableView view,
        TableView table,
        IFileManagerMessenger messenger,
        FileEntryTableNavigationState navigationState)
    {
        View = view;
        Table = table;
        Messenger = messenger;
        NavigationState = navigationState;
    }

    /// <summary>The owning view; exposes the pane <see cref="SpecFileEntryTableView.Identity"/> and
    /// the <see cref="FrameworkElement.DispatcherQueue"/> used to marshal UI work.</summary>
    public SpecFileEntryTableView View { get; }

    /// <summary>The virtualised <see cref="TableView"/> that actually renders the rows.</summary>
    public TableView Table { get; }

    /// <summary>The pane-scoped messenger wrapper used for register/send; behaviors filter incoming
    /// messages by <see cref="View"/> identity through wrapper overloads.</summary>
    public IFileManagerMessenger Messenger { get; }

    /// <summary>Shared selection/navigation cursor state used by the keyboard behaviors.</summary>
    public FileEntryTableNavigationState NavigationState { get; }

    /// <summary>Snapshots the current rows as a new list (UI thread; allocates per call).</summary>
    public IReadOnlyList<SpecFileEntryViewModel> GetItems() => Table.Items.OfType<SpecFileEntryViewModel>().ToList();

    /// <summary>Snapshots the currently selected rows as a new list (UI thread; allocates per call).</summary>
    public IReadOnlyList<SpecFileEntryViewModel> GetSelectedItems() => Table.SelectedItems.OfType<SpecFileEntryViewModel>().ToList();

    /// <summary>
    /// Builds a context from a loaded view, validating that identity, table, and messenger are all set.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the view's
    /// <see cref="SpecFileEntryTableView.Identity"/>, <see cref="SpecFileEntryTableView.Table"/>, or
    /// <see cref="SpecFileEntryTableView.Messenger"/> has not been set yet.</exception>
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
