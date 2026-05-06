namespace WinUiFileManager.Presentation.ViewModels.FileInspector;

internal sealed class CloudFileInspectorCategory : IFileInspectorCategoryProvider
{
    public string Category => "Cloud";

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new("Cloud", "Status", "Combined cloud-file state summary such as hydrated, dehydrated, pinned, synced, or uploading.", 0),
        new("Cloud", "Provider", "Cloud provider display name.", 1),
        new("Cloud", "Sync Root", "Owning sync-root path or display name.", 2),
        new("Cloud", "Root ID", "Sync-root registration identifier.", 3),
        new("Cloud", "Provider ID", "Provider identifier from the sync-root registration.", 4),
        new("Cloud", "Available", "Whether the selected item is currently available locally.", 5),
        new("Cloud", "Transfer", "Current transfer state such as upload, download, or paused, when Windows exposes it.", 6),
        new("Cloud", "Custom", "Provider-defined custom cloud status text, when available.", 7)
    ];
}
