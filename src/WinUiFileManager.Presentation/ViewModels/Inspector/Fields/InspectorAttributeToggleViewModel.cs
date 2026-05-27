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
    private IReadOnlyList<InspectorToggleFieldViewModel> _fields = [];
    private NormalizedPath? _selectedPath;

    public InspectorAttributeToggleViewModel(IMessenger messenger) => _messenger = messenger;

    public void Initialize(IReadOnlyList<InspectorCategoryViewModel> categories)
    {
        _fields = categories
            .SelectMany(static category => category.Fields)
            .OfType<InspectorToggleFieldViewModel>()
            .Where(static field => ToggleableFlags.ContainsKey(field.Key))
            .ToArray();

        foreach (var field in _fields)
        {
            field.ConfigureRefreshDrivenToggle(enabled => ToggleAttributeAsync(field.Key, enabled));
        }
    }

    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedPath = selectedItem?.FullPath;
        foreach (var field in _fields)
        {
            field.ResetToggleCommand();
        }
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
