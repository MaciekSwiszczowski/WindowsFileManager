using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

public interface IFileIdentityInterop
{
    FileIdResult GetFileId(string path);

    FileIdentityDetailsResult GetIdentityDetails(string path);

    FileLinkDiagnosticsResult GetLinkDiagnostics(string path);

    FileStreamDiagnosticsResult GetStreamDiagnostics(string path);

    FileSecurityDiagnosticsResult GetSecurityDiagnostics(string path);

    FileThumbnailDiagnosticsResult GetThumbnailDiagnostics(string path);

    FileLockDiagnosticsResult GetLockDiagnostics(string path);
}
