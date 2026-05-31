using WinUiFileManager.Application.Messages.RequestMessages.FileOperations;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Connects the inspector's writable attribute toggles (Read Only, Hidden, Archive) to the file-operation layer.
/// On init it finds the matching <see cref="InspectorToggleFieldViewModel"/>s and wires each to dispatch a
/// <see cref="SetFileAttributeFlagRequestedMessage"/> for the currently selected path when flipped.
/// </summary>
/// <remarks>
/// Only sends messages (no registrations), so there is nothing to dispose. The toggles are refresh-driven: this
/// dispatches the change request but does not update field values; the resulting diagnostics refresh does.
/// </remarks>
public sealed class InspectorAttributeToggleViewModel
{
    /// <summary>Maps the toggleable field keys to their corresponding <see cref="FileAttributes"/> flag.</summary>
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

    /// <summary>Locates the toggleable attribute fields in the category tree and wires their toggle commands.</summary>
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

    /// <summary>Sets the target path for subsequent toggles (null clears it) and re-enables each toggle for the new item.</summary>
    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedPath = selectedItem?.FullPath;
        foreach (var field in _fields)
        {
            field.ResetToggleCommand();
        }
    }

    /// <summary>
    /// Dispatches a request to set/clear the attribute flag for the given field key on the selected path. No-op
    /// (completed task) when there is no selected path or the key is not toggleable. Returns a completed task
    /// because dispatch is synchronous; the toggle's callback signature is async by contract.
    /// </summary>
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
