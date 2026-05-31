using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.Views;

/// <summary>
/// Code-behind for a single file pane: hosts the <see cref="SpecFileEntryTableView"/>, the path text
/// box, and the drive selector, and tracks pane focus so it can publish focus / inspector-refresh
/// messages. Wires the table's <see cref="SpecFileEntryTableView.Identity"/> and messenger from the VM.
/// </summary>
/// <remarks>
/// This view is the disposal model the others should follow (AGENTS.md §5): it implements
/// <see cref="IDisposable"/> and every subscription made in the constructor — the
/// <see cref="UIElement.PointerPressedEvent"/> handler (stored in <see cref="_panelPointerPressedHandler"/>
/// so the exact delegate can be removed), <c>GettingFocus</c>, and <c>LosingFocus</c> — is reversed in
/// <see cref="Dispose"/>. <see cref="Dispose"/> is reachable both from <see cref="OnUnloaded"/> and from
/// the parent <c>PanelsView</c> disposing its children, and is idempotent via <see cref="_disposed"/>.
/// (The <c>Unloaded += OnUnloaded</c> subscription itself is not reversed, but it is self-targeted.)
/// </remarks>
public sealed partial class SinglePanelView : IDisposable
{
    // Stored so the exact same delegate instance can be passed to RemoveHandler in Dispose.
    private readonly PointerEventHandler _panelPointerPressedHandler;
    private string? _identity;
    private bool _panelFocused;
    private bool _disposed;

    public SinglePanelView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;

        // handledEventsToo so a click that the table handles still counts as focusing this pane.
        _panelPointerPressedHandler = OnPanelPointerPressed;
        PanelBorder.AddHandler(PointerPressedEvent, _panelPointerPressedHandler, handledEventsToo: true);
        GettingFocus += OnPanelGettingFocus;
        LosingFocus += OnPanelLosingFocus;
    }

    /// <exception cref="InvalidOperationException">Thrown when read before <see cref="Initialize"/>.</exception>
    private string Identity => _identity ?? throw new InvalidOperationException("SinglePanel must be initialized with Identity.");

    /// <summary>The bound view model. Assigning it refreshes the x:Bind bindings.</summary>
    /// <exception cref="InvalidOperationException">Thrown when read before <see cref="Initialize"/>.</exception>
    public PanelViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(SinglePanelView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    /// <summary>The hosted file table, exposed so the parent can move focus to it.</summary>
    public SpecFileEntryTableView Table => EntryTable;

    /// <summary>Binds the pane to its VM and stamps the table's identity/messenger. Identity is
    /// write-once.</summary>
    /// <exception cref="InvalidOperationException">Thrown when called again with a different identity.</exception>
    public void Initialize(PanelViewModel viewModel)
    {
        if (_identity is not null && _identity != viewModel.Identity)
        {
            throw new InvalidOperationException("Identity cannot be changed once set.");
        }

        _identity = viewModel.Identity;
        ViewModel = viewModel;
        EntryTable.Identity = Identity;
        EntryTable.Messenger = viewModel.Messenger;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Dispose();

    /// <summary>Reverses all constructor event subscriptions. Idempotent; safe to call from both
    /// <see cref="OnUnloaded"/> and the parent's explicit disposal.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        PanelBorder.RemoveHandler(PointerPressedEvent, _panelPointerPressedHandler);
        GettingFocus -= OnPanelGettingFocus;
        LosingFocus -= OnPanelLosingFocus;
    }

    /// <summary>Drive selector changed: if the new volume isn't where we already are, navigate to its
    /// root. Guarded so reselecting the current drive does not trigger a redundant navigation.</summary>
    private void OnDriveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: VolumeInfo volume }
            || IsCurrentPathOnVolume(volume))
        {
            return;
        }

        ViewModel.FileEntries.EditablePath = volume.RootPath.DisplayPath;
        CommitPath(volume.RootPath.DisplayPath);
    }

    /// <summary>Path box keys: Escape reverts the editable path to the current path; Enter commits it.</summary>
    private void OnPathTextBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.FileEntries.EditablePath = ViewModel.FileEntries.CurrentPath;
            ViewModel.FileEntries.PathValidationMessage = string.Empty;
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        CommitPath(ViewModel.FileEntries.EditablePath);
    }

    /// <summary>Sends a navigate-to-path request for this pane and clears any validation message.</summary>
    private void CommitPath(string rawPath)
    {
        ViewModel.Messenger.Send(new FileTableNavigateToPathRequestedMessage(Identity, NormalizedPath.FromUserInput(rawPath)));
        ViewModel.FileEntries.PathValidationMessage = string.Empty;
    }

    private VolumeInfo? FindVolume(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var volume in ViewModel.AvailableVolumes)
        {
            var root = volume.RootPath.DisplayPath;
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return volume;
            }
        }

        return null;
    }

    private bool IsCurrentPathOnVolume(VolumeInfo volume)
    {
        var currentPath = ViewModel.FileEntries.CurrentPath;
        return !string.IsNullOrWhiteSpace(currentPath)
            && currentPath.StartsWith(volume.RootPath.DisplayPath, StringComparison.OrdinalIgnoreCase);
    }

    private void OnPanelPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PublishMessagesOnFocusChanged(isFocused: true);
    }

    // Focus entering any descendant of this pane counts as the pane becoming focused.
    private void OnPanelGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: true);
        }
    }

    // Focus moving to an element outside this pane counts as the pane losing focus.
    private void OnPanelLosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (!ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: false);
        }
    }

    /// <summary>Walks up from <paramref name="focusedElement"/> to determine whether it is this pane or
    /// a descendant of it.</summary>
    private bool ContainsFocusedElement(object? focusedElement)
    {
        var current = focusedElement as DependencyObject;
        while (current is not null)
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>Publishes a <see cref="FileTableFocusedMessage"/> when the pane's focus state changes
    /// (and a refresh-inspector request on focus gain). De-duplicates redundant notifications, but still
    /// re-asserts focus when the VM does not yet consider this pane active.</summary>
    private void PublishMessagesOnFocusChanged(bool isFocused)
    {
        if (_panelFocused == isFocused && (!isFocused || ViewModel.IsActive))
        {
            return;
        }

        _panelFocused = isFocused;
        ViewModel.Messenger.Send(new FileTableFocusedMessage(Identity, isFocused));
        if (isFocused)
        {
            ViewModel.Messenger.Send(new RefreshInspectorRequestMessage());
        }
    }
}
