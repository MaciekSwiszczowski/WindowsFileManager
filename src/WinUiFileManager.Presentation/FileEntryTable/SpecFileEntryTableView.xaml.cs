using ObservableCollections;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// The user control that hosts one pane's virtualised file <see cref="TableView"/>. It is the host the
/// <see cref="Behaviors.FileEntryTableBehaviorBase"/> behaviors attach to; it exposes the underlying
/// table, the pane <see cref="Identity"/>, the <see cref="Messenger"/>, and the shared
/// <see cref="NavigationState"/> that the behaviors coordinate through, and it owns the double-tap →
/// navigate gesture.
/// </summary>
/// <remarks>
/// <see cref="Identity"/> and <see cref="Messenger"/> must be assigned by the host before the control
/// loads; <see cref="SpecFileEntryTableView_Loaded"/> enforces this. The XAML keeps
/// <c>CacheLength="1.0"</c> and virtualization on (AGENTS.md §3) — this code-behind must not realise all
/// rows or add per-row state.
/// <para>
/// Leak note (AGENTS.md §5): the constructor subscribes <c>Loaded</c> and adds a
/// <see cref="UIElement.DoubleTappedEvent"/> handler but never removes them on <c>Unloaded</c>. Because
/// these target <c>this</c> (the handlers do not outlive the control), they are collected with the
/// control rather than rooting a longer-lived object; still, they are unbalanced subscriptions.
/// </para>
/// </remarks>
public sealed partial class SpecFileEntryTableView
{
    /// <summary>Backing DP for <see cref="ItemsSource"/>, the pane's row collection.</summary>
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(NotifyCollectionChangedSynchronizedViewList<FileListingRow>),
            typeof(SpecFileEntryTableView), new PropertyMetadata(null));

    /// <summary>Backing DP for <see cref="CurrentFolder"/>, the display path currently shown.</summary>
    public static readonly DependencyProperty CurrentFolderProperty =
        DependencyProperty.Register(
            nameof(CurrentFolder),
            typeof(string),
            typeof(SpecFileEntryTableView),
            new PropertyMetadata(string.Empty));

    public SpecFileEntryTableView()
    {
        InitializeComponent();
        Loaded += SpecFileEntryTableView_Loaded;
        // handledEventsToo: true so a double-tap on a row still triggers navigation even though the
        // TableView marks the pointer gesture handled. NOTE: not removed on Unloaded (see class remarks).
        AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(EntryTable_DoubleTapped), handledEventsToo: true);
    }

    /// <summary>The rows to display. Bound from the pane view model; a synchronized notify-adapter over the
    /// data source's row store so the table reacts to scan/watcher/sort updates.</summary>
    public NotifyCollectionChangedSynchronizedViewList<FileListingRow>? ItemsSource
    {
        get => (NotifyCollectionChangedSynchronizedViewList<FileListingRow>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>The pane-scoped messenger used by this view and its behaviors. Must be set by the host
    /// before load (plain CLR property, not a DP, because it is wired in code, not XAML data-binding).</summary>
    public IFileManagerMessenger? Messenger { get; set; }

    /// <summary>The folder path currently displayed; compared by file-operation behaviors to decide
    /// whether a request targets this pane.</summary>
    public string CurrentFolder
    {
        get => (string)GetValue(CurrentFolderProperty);
        set => SetValue(CurrentFolderProperty, value);
    }

    /// <summary>The underlying virtualised <see cref="TableView"/> (the XAML-named <c>EntryTable</c>).</summary>
    public TableView Table => EntryTable;

    /// <summary>The shared navigation/selection cursor state for this pane's table.</summary>
    public FileEntryTableNavigationState NavigationState { get; } = new();

    /// <summary>The pane identity (e.g. left/right) used by wrapper registrations to scope messages;
    /// must be assigned before the control loads.</summary>
    public string Identity { get; set; } = string.Empty;

    private void SpecFileEntryTableView_Loaded(object sender, RoutedEventArgs e)
    {
        // Fail fast if the host forgot to wire identity/messenger — behaviors depend on both.
        if (string.IsNullOrWhiteSpace(Identity))
        {
            throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Identity)} must be set.");
        }

        _ = GetRequiredMessenger();
    }

    /// <summary>Turns a double-tap on a row into a navigate-up (parent row) or navigate-down (directory)
    /// request message. Files are ignored. UI-thread event handler.</summary>
    private void EntryTable_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject).FindItem() is not { } item)
        {
            return;
        }

        var messenger = GetRequiredMessenger();

        if (FileListingRow.IsParentEntry(item))
        {
            messenger.Send(new FileTableNavigateUpRequestedMessage(Identity));
            e.Handled = true;
            return;
        }

        if (item.Model is { Kind: ItemKind.Directory } model)
        {
            messenger.Send(new FileTableNavigateDownRequestedMessage(Identity, model.Name));
            e.Handled = true;
        }
    }

    /// <summary>Returns the assigned <see cref="Messenger"/>.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the host did not assign a messenger.</exception>
    private IFileManagerMessenger GetRequiredMessenger() =>
        Messenger
        ?? throw new InvalidOperationException($"{nameof(SpecFileEntryTableView)}.{nameof(Messenger)} must be set.");

}
