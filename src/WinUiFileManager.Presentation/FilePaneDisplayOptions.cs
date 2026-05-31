namespace WinUiFileManager.Presentation;

/// <summary>
/// Process-wide display feature toggles for the file panes. A simple static settings holder consulted by
/// the file-table views/behaviors; not persisted.
/// </summary>
public static class FilePaneDisplayOptions
{
    /// <summary>Whether interactive column resizing is enabled in the file table. Defaults to true.</summary>
    public static bool EnableColumnResize { get; set; } = true;
}
