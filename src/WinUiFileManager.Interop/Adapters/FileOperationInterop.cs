#pragma warning disable RS0030 // FileOperationInterop is the approved boundary for direct System.IO copy/move calls until CopyFile2 lands.
using System.ComponentModel;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

internal sealed class FileOperationInterop : IFileOperationInterop
{
    public InteropResult CopyFile(string source, string destination, bool overwrite)
    {
        try
        {
            File.Copy(source, destination, overwrite);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult MoveFile(string source, string destination, bool overwrite)
    {
        try
        {
            File.Move(source, destination, overwrite);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult MoveDirectory(string source, string destination)
    {
        try
        {
            Directory.Move(source, destination);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult RemoveDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: false);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult CreateDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }

    public InteropResult SetFileAttributes(string path, uint attributes)
    {
        try
        {
            File.SetAttributes(path, (FileAttributes)attributes);
            return InteropResult.Ok();
        }
        catch (IOException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return InteropResult.Fail(ex.HResult, ex.Message);
        }
    }
}
