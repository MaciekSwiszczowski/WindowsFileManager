namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// Supplies cached text projections used by the file-listing hot path without making the listing engine
/// depend on Presentation converters or services. Presentation provides the process-wide implementation.
/// </summary>
public interface IFileListingStringCache
{
    /// <summary>Returns a shared extension string when it is worth caching, otherwise the original value.</summary>
    public string GetExtension(string extension);

    /// <summary>Returns the compact table attribute text used for sort comparisons and table display.</summary>
    public string GetTableAttributes(FileAttributes attributes);
}
