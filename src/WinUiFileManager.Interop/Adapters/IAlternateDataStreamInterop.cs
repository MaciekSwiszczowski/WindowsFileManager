namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Narrow abstraction exposing only NTFS alternate-data-stream enumeration, so consumers that just need the
/// ADS list don't depend on the broader <see cref="IFileSystemMetadataInterop"/>. Both are implemented by the
/// same <see cref="FileSystemMetadataInterop"/> type (interface-segregation over a shared implementation).
/// </summary>
public interface IAlternateDataStreamInterop
{
    /// <summary>Returns one display line ("name (N bytes)") per alternate data stream; empty when none.</summary>
    /// <param name="path">Path to the file to inspect.</param>
    IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path);
}
