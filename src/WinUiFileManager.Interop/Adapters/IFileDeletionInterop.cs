using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Win32 deletion primitives used by higher layers that need forceful file-system cleanup without importing
/// <c>Windows.Win32.*</c> directly. Implemented by the Interop layer with CsWin32 bindings.
/// </summary>
public interface IFileDeletionInterop
{
    /// <summary>Sets native file attributes for a file-system entry.</summary>
    /// <param name="path">File or directory path to modify.</param>
    /// <param name="attributes">Replacement attributes to apply.</param>
    /// <returns>Success or the captured Win32 failure.</returns>
    InteropResult SetAttributes(string path, FileAttributes attributes);

    /// <summary>Deletes one file or file-like reparse point.</summary>
    /// <param name="path">Path to delete.</param>
    /// <returns>Success or the captured Win32 failure.</returns>
    public InteropResult DeleteFile(string path);

    /// <summary>Removes one empty directory or directory-like reparse point.</summary>
    /// <param name="path">Directory path to remove.</param>
    /// <returns>Success or the captured Win32 failure.</returns>
    public InteropResult RemoveDirectory(string path);
}
