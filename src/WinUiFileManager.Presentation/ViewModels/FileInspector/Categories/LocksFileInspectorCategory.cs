namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class LocksFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Locks";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Locks", "Is locked", "Whether the selected item appears to be locked based on the other lock diagnostics in this category.", 0),
        new("Locks", "In Use", "Whether Windows currently reports the item as in use. Best-effort diagnostic.", 1),
        new("Locks", "Locked By", "Applications or services that Windows reports as using this item.", 2),
        new("Locks", "Lock PIDs", "Process IDs of applications using this item. Useful in Task Manager or Process Explorer.", 3),
        new("Locks", "Lock Services", "Service names associated with the lock, when available.", 4)
    ];
}
