using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public interface IFileOperationInterop
{
    InteropResult CopyFile(string source, string destination, bool overwrite);
    InteropResult MoveFile(string source, string destination, bool overwrite);
    InteropResult MoveDirectory(string source, string destination);
    InteropResult DeleteFile(string path);
    InteropResult RemoveDirectory(string path);
    InteropResult CreateDirectory(string path);
    InteropResult SetFileAttributes(string path, uint attributes);
}
