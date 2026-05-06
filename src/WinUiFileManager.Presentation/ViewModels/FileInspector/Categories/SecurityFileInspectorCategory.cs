using static WinUiFileManager.Presentation.ViewModels.FileInspectorCategory;

namespace WinUiFileManager.Presentation.ViewModels.FileInspector.Categories;

internal sealed class SecurityFileInspectorCategory : IFileInspectorCategoryProvider
{
    public FileInspectorCategory Category => Security;

    public IReadOnlyList<FileInspectorFieldDefinition> Fields { get; } =
    [
        new(Security, "Owner", "Owner of the file or folder.", 0),
        new(Security, "Group", "Primary group of the file or folder.", 1),
        new(Security, "DACL Summary", "Summary of access rules from the discretionary access control list.", 2),
        new(Security, "SACL Summary", "Summary of audit rules from the system access control list.", 3),
        new(Security, "Inherited", "Whether the permissions are inherited.", 4),
        new(Security, "Protected", "Whether inherited permissions are blocked.", 5)
    ];
}
