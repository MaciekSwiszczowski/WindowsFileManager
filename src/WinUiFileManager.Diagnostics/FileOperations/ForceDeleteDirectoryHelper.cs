using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Diagnostics.FileOperations;

/// <summary>
/// Diagnostics-layer helper that forcefully deletes a directory tree one entry at a time by clearing blocking
/// attributes and delegating native delete/remove calls to the Interop layer.
/// </summary>
/// <remarks>
/// Reparse-point directories are removed as links rather than traversed, so a junction or symlink inside the
/// target folder does not cause deletion of its external target. Locked files can still fail when the owner did
/// not grant delete sharing; this helper makes the strongest immediate delete attempt available without scheduling
/// reboot-time deletion.
/// </remarks>
public sealed class ForceDeleteDirectoryHelper
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    private readonly IFileDeletionInterop _fileDeletionInterop;

    public ForceDeleteDirectoryHelper(IFileDeletionInterop fileDeletionInterop)
    {
        _fileDeletionInterop = fileDeletionInterop;
    }

    /// <summary>
    /// Deletes <paramref name="directoryPath"/> and all entries below it. Missing directories are ignored.
    /// </summary>
    /// <param name="directoryPath">Root directory to remove.</param>
    /// <exception cref="IOException">A native delete/remove operation failed.</exception>
    public void DeleteDirectoryTree(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        DeleteDirectoryContents(directoryPath);
        RemoveDirectory(directoryPath);
    }

    private void DeleteDirectoryContents(string directoryPath)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(directoryPath, "*", EnumerationOptions))
        {
            var attributes = File.GetAttributes(entryPath);
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                if (!attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    DeleteDirectoryContents(entryPath);
                }

                RemoveDirectory(entryPath);
            }
            else
            {
                DeleteFile(entryPath);
            }
        }
    }

    private void DeleteFile(string filePath)
    {
        EnsureSuccess(_fileDeletionInterop.SetAttributes(filePath, FileAttributes.Normal));
        EnsureSuccess(_fileDeletionInterop.DeleteFile(filePath));
    }

    private void RemoveDirectory(string directoryPath)
    {
        EnsureSuccess(_fileDeletionInterop.SetAttributes(directoryPath, FileAttributes.Directory));
        EnsureSuccess(_fileDeletionInterop.RemoveDirectory(directoryPath));
    }

    private static void EnsureSuccess(InteropResult result)
    {
        if (!result.Success)
        {
            throw new IOException(result.ErrorMessage, result.NativeErrorCode);
        }
    }
}
