using Microsoft.Extensions.Logging;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.FileSystem;

public sealed class WindowsFileSystemService : IFileSystemService
{
    private readonly IPathNormalizationService _pathService;
    private readonly IFileIdentityInterop _fileIdentityInterop;
    private readonly ILogger<WindowsFileSystemService> _logger;

    public WindowsFileSystemService(
        IPathNormalizationService pathService,
        IFileIdentityInterop fileIdentityInterop,
        ILogger<WindowsFileSystemService> logger)
    {
        _pathService = pathService;
        _fileIdentityInterop = fileIdentityInterop;
        _logger = logger;
    }

    public Task<IReadOnlyList<FileSystemEntryModel>> EnumerateDirectoryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        var dirInfo = new DirectoryInfo(path.DisplayPath);

        if (!dirInfo.Exists)
        {
            _logger.LogWarning("Directory does not exist: {Path}", path.DisplayPath);
            return Task.FromResult<IReadOnlyList<FileSystemEntryModel>>([]);
        }

        var entries = new List<FileSystemEntryModel>();

        foreach (var fsi in dirInfo.EnumerateFileSystemInfos())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                entries.Add(BuildEntryModel(fsi));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Skipping inaccessible entry: {Path}", fsi.FullName);
            }
        }

        return Task.FromResult<IReadOnlyList<FileSystemEntryModel>>(entries);
    }

    public Task<FileSystemEntryModel?> GetEntryAsync(
        NormalizedPath path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var displayPath = path.DisplayPath;
        FileSystemInfo? fsi = null;

        if (File.Exists(displayPath))
            fsi = new FileInfo(displayPath);
        else if (Directory.Exists(displayPath))
            fsi = new DirectoryInfo(displayPath);

        if (fsi is null)
            return Task.FromResult<FileSystemEntryModel?>(null);

        return Task.FromResult<FileSystemEntryModel?>(BuildEntryModel(fsi));
    }

    public Task<bool> ExistsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var displayPath = path.DisplayPath;
        return Task.FromResult(File.Exists(displayPath) || Directory.Exists(displayPath));
    }

    public Task<bool> DirectoryExistsAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(path.DisplayPath));
    }

    private FileSystemEntryModel BuildEntryModel(FileSystemInfo fsi)
    {
        var isDirectory = fsi is DirectoryInfo;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var size = fsi is FileInfo fi ? fi.Length : 0L;
        var extension = isDirectory ? string.Empty : fsi.Extension;

        var fileIdResult = _fileIdentityInterop.GetFileId(fsi.FullName);
        var fileId = fileIdResult is { Success: true, FileId128: not null }
            ? new NtfsFileId(fileIdResult.FileId128)
            : NtfsFileId.None;

        return new FileSystemEntryModel(
            FullPath: NormalizedPath.FromUserInput(fsi.FullName),
            Name: fsi.Name,
            Extension: extension,
            Kind: kind,
            Size: size,
            LastWriteTimeUtc: fsi.LastWriteTimeUtc,
            CreationTimeUtc: fsi.CreationTimeUtc,
            Attributes: fsi.Attributes,
            FileId: fileId);
    }
}
