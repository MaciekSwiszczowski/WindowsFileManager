namespace WinUiFileManager.Application.Diagnostics;

public sealed class FileInspectorInteropOptions
{
    public static FileInspectorInteropOptions AllEnabled { get; } =
        new(FileInspectorInteropCategories.All);

    public FileInspectorInteropOptions(FileInspectorInteropCategories enabledCategories)
    {
        EnabledCategories = enabledCategories;
    }

    public FileInspectorInteropCategories EnabledCategories { get; }

    public bool IsEnabled(FileInspectorInteropCategories category) =>
        (EnabledCategories & category) == category;
}
