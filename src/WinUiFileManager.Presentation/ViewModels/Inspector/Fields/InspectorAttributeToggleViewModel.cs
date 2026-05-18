using WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

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

    private readonly IMessenger _messenger;
    private IReadOnlyDictionary<string, InspectorFieldViewModel> _fields = new Dictionary<string, InspectorFieldViewModel>();
    private NormalizedPath? _selectedPath;

    public InspectorAttributeToggleViewModel(IMessenger messenger)
    {
        _messenger = messenger;
    }

    public void Initialize(IReadOnlyList<InspectorCategoryViewModel> categories)
    {
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var field in _fields.Values.OfType<InspectorToggleFieldViewModel>())
        {
            if (ToggleableFlags.ContainsKey(field.Key))
            {
                field.ConfigureRefreshDrivenToggle(enabled => ToggleAttributeAsync(field.Key, enabled));
            }
        }
    }

    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedPath = selectedItem?.FullPath;
    }

    private Task ToggleAttributeAsync(string key, bool enabled)
    {
        if (_selectedPath is not { } path
            || !ToggleableFlags.TryGetValue(key, out var flag))
        {
            return Task.CompletedTask;
        }

        _messenger.Send(new SetFileAttributeFlagRequestedMessage(path, flag, enabled));
        return Task.CompletedTask;
    }
}
