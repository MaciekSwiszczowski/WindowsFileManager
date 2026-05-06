namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

public sealed class FileInspectorCommandsViewModel
{
    private readonly IClipboardService _clipboardService;
    private readonly IShellService _shellService;
    private readonly Func<string> _currentFullPath;
    private readonly Func<IReadOnlyList<FileInspectorCategoryViewModel>> _categories;
    private readonly Func<IReadOnlyList<FileInspectorFieldViewModel>> _fields;
    private readonly Func<FileTableSelectionChangedMessage?> _createRefreshMessage;

    public FileInspectorCommandsViewModel(
        IClipboardService clipboardService,
        IShellService shellService,
        Func<string> currentFullPath,
        Func<IReadOnlyList<FileInspectorCategoryViewModel>> categories,
        Func<IReadOnlyList<FileInspectorFieldViewModel>> fields,
        Func<FileTableSelectionChangedMessage?> createRefreshMessage)
    {
        _clipboardService = clipboardService;
        _shellService = shellService;
        _currentFullPath = currentFullPath;
        _categories = categories;
        _fields = fields;
        _createRefreshMessage = createRefreshMessage;

        CopyAllCommand = new AsyncRelayCommand(CopyAllAsync);
        RefreshCommand = new RelayCommand(Refresh);
        ShowPropertiesCommand = new AsyncRelayCommand(ShowPropertiesAsync);
    }

    public IAsyncRelayCommand CopyAllCommand { get; }

    public IRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ShowPropertiesCommand { get; }

    private async Task CopyAllAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFullPath()))
        {
            return;
        }

        var builder = new StringBuilder();
        foreach (var grouping in _categories()
            .Where(static category => category.HasVisibleFields)
            .OrderBy(static category => FileInspectorCategorySort.GetSortOrder(category.Category)))
        {
            builder.AppendLine(grouping.Name);
            foreach (var field in _fields()
                .Where(field => field.IsVisible && field.Category == grouping.Category)
                .OrderBy(static field => field.SortOrder))
            {
                builder.Append("  ").Append(field.Key).Append(": ").AppendLine(field.Value);
            }

            builder.AppendLine();
        }

        await _clipboardService.SetTextAsync(builder.ToString().TrimEnd(), CancellationToken.None);
    }

    private void Refresh()
    {
        var message = _createRefreshMessage();
        if (message is not null)
        {
            WeakReferenceMessenger.Default.Send(message);
        }
    }

    private async Task ShowPropertiesAsync()
    {
        var currentFullPath = _currentFullPath();
        if (string.IsNullOrWhiteSpace(currentFullPath))
        {
            return;
        }

        await _shellService.ShowPropertiesAsync(
            NormalizedPath.FromUserInput(currentFullPath),
            CancellationToken.None);
    }
}
