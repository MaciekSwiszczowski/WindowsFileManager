namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed class InspectorAttributeToggleViewModel
{
    private static readonly IReadOnlyDictionary<string, FileAttributes> ToggleableFlags =
        new Dictionary<string, FileAttributes>(StringComparer.OrdinalIgnoreCase)
        {
            ["Read Only"] = FileAttributes.ReadOnly,
            ["Hidden"] = FileAttributes.Hidden,
            ["Archive"] = FileAttributes.Archive
    };

    private readonly IFileIdentityService _fileIdentityService;
    private readonly ILogger<InspectorAttributeToggleViewModel> _logger;
    private IReadOnlyDictionary<string, InspectorFieldViewModel> _fields = new Dictionary<string, InspectorFieldViewModel>();
    private string _selectedPath = string.Empty;
    private FileAttributes _selectedAttributes;
    private Action _refreshSearch = static () => { };

    public InspectorAttributeToggleViewModel(
        IFileIdentityService fileIdentityService,
        ILogger<InspectorAttributeToggleViewModel> logger)
    {
        _fileIdentityService = fileIdentityService;
        _logger = logger;
    }

    public void Initialize(IReadOnlyList<InspectorCategoryViewModel> categories, Action refreshSearch)
    {
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);
        _refreshSearch = refreshSearch;

        foreach (var field in _fields.Values.OfType<InspectorToggleFieldViewModel>())
        {
            if (ToggleableFlags.ContainsKey(field.Key))
            {
                field.ConfigureToggle(enabled => ToggleAttributeAsync(field.Key, enabled));
            }
        }
    }

    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedPath = selectedItem?.FullPath.DisplayPath ?? string.Empty;
        _selectedAttributes = selectedItem?.Attributes ?? FileAttributes.None;
    }

    private async Task<bool> ToggleAttributeAsync(string key, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(_selectedPath)
            || !ToggleableFlags.TryGetValue(key, out var flag))
        {
            return false;
        }

        try
        {
            var updated = await _fileIdentityService.SetNtfsAttributeFlagAsync(
                _selectedPath,
                flag,
                enabled,
                CancellationToken.None);

            if (updated)
            {
                UpdateDisplayedAttributes(flag, enabled);
                _refreshSearch();
            }

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle NTFS flag {Flag} for {Path}", key, _selectedPath);
            return false;
        }
    }

    private void UpdateDisplayedAttributes(FileAttributes flag, bool enabled)
    {
        var updatedAttributes = enabled
            ? _selectedAttributes | flag
            : _selectedAttributes & ~flag;

        _selectedAttributes = updatedAttributes;

        if (_fields.TryGetValue("Attributes", out var attributesField))
        {
            attributesField.Value = updatedAttributes.ToString();
        }
    }
}
