using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.FileSystem;

public sealed class NtfsFileIdentityService : IFileIdentityService
{
    private const int FindStreamInfoStandard = 0;
    private const uint GetFinalPathNameNormalized = 0;

    private readonly IFileIdentityInterop _fileIdentityInterop;

    public NtfsFileIdentityService(IFileIdentityInterop fileIdentityInterop)
    {
        _fileIdentityInterop = fileIdentityInterop;
    }

    public Task<NtfsFileId> GetFileIdAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _fileIdentityInterop.GetFileId(path);

        var fileId = result is { Success: true, FileId128: not null }
            ? new NtfsFileId(result.FileId128)
            : NtfsFileId.None;

        return Task.FromResult(fileId);
    }

    public Task<FileIdentityDetails> GetIdentityDetailsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fileIdResult = _fileIdentityInterop.GetFileId(path);
            var fileId = fileIdResult is { Success: true, FileId128: not null }
                ? new NtfsFileId(fileIdResult.FileId128)
                : NtfsFileId.None;

            using var stream = OpenReadHandle(path);
            var handle = stream.SafeFileHandle.DangerousGetHandle();

            var legacyInfo = TryGetLegacyFileInfo(handle, out var legacyError);
            if (!string.IsNullOrWhiteSpace(legacyError))
            {
                _ = legacyError;
            }

            var volumeSerial = TryGetVolumeSerial(path);
            var finalPath = TryGetFinalPath(handle) ?? Path.GetFullPath(path);

            return Task.FromResult(new FileIdentityDetails(
                fileId,
                volumeSerial ?? string.Empty,
                legacyInfo is null
                    ? string.Empty
                    : $"0x{((ulong)legacyInfo.Value.nFileIndexHigh << 32 | legacyInfo.Value.nFileIndexLow):X16}",
                legacyInfo is null ? string.Empty : legacyInfo.Value.nNumberOfLinks.ToString(),
                finalPath));
        }
        catch (Exception)
        {
            return Task.FromResult(new FileIdentityDetails(
                NtfsFileId.None,
                string.Empty,
                string.Empty,
                string.Empty,
                path))
            ;
        }
    }

    public Task<FileLinkDiagnosticsDetails> GetLinkDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            FileSystemInfo fsi = File.Exists(path) ? new FileInfo(path) : new DirectoryInfo(path);
            var linkTarget = fsi.LinkTarget ?? string.Empty;
            var linkStatus = string.Empty;
            if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                linkStatus = "Shell shortcut";
            }

            var reparseTag = fsi.Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint)
                ? "Reparse point"
                : string.Empty;

            return Task.FromResult(new FileLinkDiagnosticsDetails(
                linkTarget,
                linkStatus,
                reparseTag,
                string.Empty,
                string.Empty));
        }
        catch (Exception)
        {
            return Task.FromResult(new FileLinkDiagnosticsDetails(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty));
        }
    }

    public Task<FileStreamDiagnosticsDetails> GetStreamDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var streams = new List<string>();
            var handle = FindFirstStreamW(path, FindStreamInfoStandard, out var data, 0);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                return Task.FromResult(new FileStreamDiagnosticsDetails("0", streams));
            }

            try
            {
                AddStreamName(streams, data);
                while (FindNextStreamW(handle, out data))
                {
                    AddStreamName(streams, data);
                }
            }
            finally
            {
                _ = FindClose(handle);
            }

            return Task.FromResult(new FileStreamDiagnosticsDetails(streams.Count.ToString(), streams));
        }
        catch (Exception)
        {
            return Task.FromResult(new FileStreamDiagnosticsDetails("0", []));
        }
    }

    public Task<FileSecurityDiagnosticsDetails> GetSecurityDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            FileSystemSecurity security = Directory.Exists(path)
                ? new DirectoryInfo(path).GetAccessControl()
                : new FileInfo(path).GetAccessControl();

            var owner = SafeIdentityToString(() => security.GetOwner(typeof(NTAccount)));
            var group = SafeIdentityToString(() => security.GetGroup(typeof(NTAccount)));
            var daclSummary = SummarizeAccessRules(security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>());

            string saclSummary;
            try
            {
                saclSummary = SummarizeAuditRules(security.GetAuditRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAuditRule>());
            }
            catch
            {
                saclSummary = string.Empty;
            }

            return Task.FromResult(new FileSecurityDiagnosticsDetails(
                owner,
                group,
                daclSummary,
                saclSummary,
                security.AreAccessRulesProtected ? false : true,
                security.AreAccessRulesProtected));
        }
        catch (Exception)
        {
            return Task.FromResult(new FileSecurityDiagnosticsDetails(string.Empty, string.Empty, string.Empty, string.Empty, null, null));
        }
    }

    public async Task<FileThumbnailDiagnosticsDetails> GetThumbnailDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var progId = Path.GetExtension(path);
            byte[]? thumbnailBytes = null;

            IStorageItem? storageItem = null;
            if (File.Exists(path))
            {
                storageItem = await StorageFile.GetFileFromPathAsync(path);
            }
            else if (Directory.Exists(path))
            {
                storageItem = await StorageFolder.GetFolderFromPathAsync(path);
            }

            if (storageItem is not null)
            {
                using var thumbnail = storageItem is StorageFile file
                    ? await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(cancellationToken)
                    : await ((StorageFolder)storageItem).GetThumbnailAsync(ThumbnailMode.SingleItem, 256).AsTask(cancellationToken);

                if (thumbnail is not null)
                {
                    thumbnail.Seek(0);
                    using var input = thumbnail.AsStreamForRead();
                    using var memory = new MemoryStream();
                    await input.CopyToAsync(memory, cancellationToken);
                    thumbnailBytes = memory.ToArray();
                }
            }

            return new FileThumbnailDiagnosticsDetails(thumbnailBytes, progId);
        }
        catch (Exception)
        {
            return new FileThumbnailDiagnosticsDetails(null, string.Empty);
        }
    }

    public Task<FileLockDiagnostics> GetLockDiagnosticsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _fileIdentityInterop.GetLockDiagnostics(path);
        if (!result.Success)
        {
            return Task.FromResult(FileLockDiagnostics.None);
        }

        var diagnostics = new FileLockDiagnostics(
            inUse: result.InUse,
            lockBy: result.LockBy,
            lockPids: result.LockPids,
            lockServices: result.LockServices,
            usage: result.Usage,
            canSwitchTo: result.CanSwitchTo,
            canClose: result.CanClose);

        return Task.FromResult(diagnostics);
    }

    private static FileStream OpenReadHandle(string path) =>
        new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1,
            FileOptions.None);

    private static BY_HANDLE_FILE_INFORMATION? TryGetLegacyFileInfo(
        IntPtr handle,
        out string? error)
    {
        error = null;
        BY_HANDLE_FILE_INFORMATION info;
        if (!GetFileInformationByHandle(handle, out info))
        {
            error = Marshal.GetLastPInvokeError().ToString();
            return null;
        }

        return info;
    }

    private static string? TryGetVolumeSerial(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        if (!GetVolumeInformationW(root, null, 0, out var serial, out _, out _, null, 0))
        {
            return null;
        }

        return serial.ToString("X8");
    }

    private static string? TryGetFinalPath(IntPtr handle)
    {
        var buffer = new StringBuilder(1024);
        var len = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, GetFinalPathNameNormalized);
        return len == 0 ? null : buffer.ToString();
    }

    private static void AddStreamName(List<string> streams, WIN32_FIND_STREAM_DATA data)
    {
        if (string.IsNullOrWhiteSpace(data.cStreamName))
        {
            return;
        }

        if (string.Equals(data.cStreamName, "::$DATA", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        streams.Add($"{data.cStreamName.Trim()} ({data.StreamSize} bytes)");
    }

    private static string SafeIdentityToString(Func<IdentityReference?> factory)
    {
        try
        {
            return factory()?.Value ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SummarizeAccessRules(IEnumerable<FileSystemAccessRule> rules)
    {
        var allow = 0;
        var deny = 0;
        var inherited = 0;

        foreach (var rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow)
            {
                allow++;
            }
            else
            {
                deny++;
            }

            if (rule.IsInherited)
            {
                inherited++;
            }
        }

        if (allow == 0 && deny == 0)
        {
            return string.Empty;
        }

        return $"Allow {allow}, Deny {deny}, Inherited {inherited}";
    }

    private static string SummarizeAuditRules(IEnumerable<FileSystemAuditRule> rules)
    {
        var success = 0;
        var failure = 0;

        foreach (var rule in rules)
        {
            if ((rule.AuditFlags & AuditFlags.Success) != 0)
            {
                success++;
            }

            if ((rule.AuditFlags & AuditFlags.Failure) != 0)
            {
                failure++;
            }
        }

        if (success == 0 && failure == 0)
        {
            return string.Empty;
        }

        return $"Success {success}, Failure {failure}";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        IntPtr hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetVolumeInformationW(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        int nFileSystemNameSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        IntPtr hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindFirstStreamW(
        string lpFileName,
        int InfoLevel,
        out WIN32_FIND_STREAM_DATA lpFindStreamData,
        int dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(
        IntPtr hFindStream,
        out WIN32_FIND_STREAM_DATA lpFindStreamData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(IntPtr hFindFile);

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint dwVolumeSerialNumber;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint nNumberOfLinks;
        public uint nFileIndexHigh;
        public uint nFileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_STREAM_DATA
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string cStreamName;
    }
}
