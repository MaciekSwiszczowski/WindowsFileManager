using WinUiFileManager.Application.Messages.RequestMessages.Navigation;
using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.Views;

public sealed partial class SinglePanelView : IDisposable
{
    private readonly PointerEventHandler _panelPointerPressedHandler;
    private string? _identity;
    private bool _panelFocused;
    private bool _disposed;

    public SinglePanelView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;

        _panelPointerPressedHandler = OnPanelPointerPressed;
        PanelBorder.AddHandler(PointerPressedEvent, _panelPointerPressedHandler, handledEventsToo: true);
        GettingFocus += OnPanelGettingFocus;
        LosingFocus += OnPanelLosingFocus;
    }

    private string Identity => _identity ?? throw new InvalidOperationException("SinglePanel must be initialized with Identity.");

    public PanelViewModel ViewModel
    {
        get => field ?? throw new InvalidOperationException($"{nameof(SinglePanelView)} must be initialized with a view model.");
        private set
        {
            field = value;
            Bindings.Update();
        }
    }

    public SpecFileEntryTableView Table => EntryTable;

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

    private void OnPanelGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: true);
        }
    }

    private void OnPanelLosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (!ContainsFocusedElement(args.NewFocusedElement))
        {
            PublishMessagesOnFocusChanged(isFocused: false);
        }
    }

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
