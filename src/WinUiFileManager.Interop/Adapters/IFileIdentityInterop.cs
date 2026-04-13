using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public interface IFileIdentityInterop
{
    FileIdResult GetFileId(string path);
}
