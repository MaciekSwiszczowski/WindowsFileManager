namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Pure formatting helpers that turn a (possibly null, i.e. parent-row) <see cref="FileSystemEntryModel"/>
/// into the strings shown in the table's Name and Modified columns. Kept off the row VM to honour the
/// "no display helpers on the row" rule (AGENTS.md §2); called on demand from cell templates.
/// </summary>
public static class SpecFileEntryDisplay
{
    /// <summary>The display name, or ".." for the synthetic parent row (null model).</summary>
    public static string GetName(FileSystemEntryModel? model) => model?.Name ?? "..";

    /// <summary>The last-write time formatted for the Modified column, or empty for the parent row.</summary>
    public static string GetModified(FileSystemEntryModel? model) =>
        model is null
            ? string.Empty
            : model.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
}
