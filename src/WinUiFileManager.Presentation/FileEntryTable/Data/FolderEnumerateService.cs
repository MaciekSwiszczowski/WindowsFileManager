using System.Collections.Concurrent;
using System.IO.Enumeration;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Subjects;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.FileEntryTable.Data;

internal sealed class FolderEnumerateService : IDisposable
{
    private static readonly ConcurrentDictionary<string, string> ExtensionPool =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false
    };

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Subject<FileSystemEntryModel> _entries = new();
    private readonly IScheduler _scheduler;
    private readonly SerialDisposable _scan = new();
    private bool _disposed;
    private bool _started;

    public FolderEnumerateService(NormalizedPath path, IScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);

        Path = path;
        _scheduler = scheduler;
    }

    public NormalizedPath Path { get; }

    public IObservable<FileSystemEntryModel> Entries => _entries;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
        {
            return;
        }

        _started = true;
        _scan.Disposable = _scheduler.Schedule(() => PumpEntries(_disposeCts.Token));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _disposeCts.Cancel();
        _scan.Dispose();
        _disposeCts.Dispose();
        _entries.Dispose();
    }

    public static FileSystemEntryModel? GetEntry(NormalizedPath path)
    {
        try
        {
            var displayPath = path.DisplayPath;
            FileSystemInfo? info = null;

            if (File.Exists(displayPath))
            {
                info = new FileInfo(displayPath);
            }
            else if (Directory.Exists(displayPath))
            {
                info = new DirectoryInfo(displayPath);
            }

            return info is null ? null : BuildEntryModel(info);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void PumpEntries(CancellationToken cancellationToken)
    {
        try
        {
            if (!Directory.Exists(Path.DisplayPath))
            {
                _entries.OnCompleted();
                return;
            }

            foreach (var entry in CreateDirectoryEnumerable(Path.DisplayPath))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _entries.OnCompleted();
                    return;
                }

                _entries.OnNext(entry);
            }

            _entries.OnCompleted();
        }
        catch (OperationCanceledException)
        {
            _entries.OnCompleted();
        }
        catch (Exception ex)
        {
            _entries.OnError(ex);
        }
    }

    private static FileSystemEntryModel BuildEntryModel(ref FileSystemEntry entry)
    {
        var fullPath = entry.ToFullPath();
        var isDirectory = entry.IsDirectory;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var name = entry.FileName.ToString();
        var extension = isDirectory ? string.Empty : System.IO.Path.GetExtension(name);

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(fullPath),
            name,
            InternExtension(extension),
            kind,
            isDirectory ? 0L : entry.Length,
            entry.LastWriteTimeUtc.UtcDateTime,
            entry.CreationTimeUtc.UtcDateTime,
            entry.Attributes);
    }

    private static FileSystemEntryModel BuildEntryModel(FileSystemInfo info)
    {
        var isDirectory = info is DirectoryInfo;
        var kind = isDirectory ? ItemKind.Directory : ItemKind.File;
        var extension = isDirectory ? string.Empty : info.Extension;

        return new FileSystemEntryModel(
            NormalizedPath.FromFullyQualifiedPath(info.FullName),
            info.Name,
            InternExtension(extension),
            kind,
            isDirectory ? 0L : ((FileInfo)info).Length,
            info.LastWriteTimeUtc,
            info.CreationTimeUtc,
            info.Attributes);
    }

    private static FileSystemEnumerable<FileSystemEntryModel> CreateDirectoryEnumerable(
        string directoryPath)
    {
        return new FileSystemEnumerable<FileSystemEntryModel>(
            directoryPath,
            static (ref entry) => BuildEntryModel(ref entry),
            EnumerationOptions);
    }

    private static string InternExtension(string extension) =>
        string.IsNullOrEmpty(extension)
            ? string.Empty
            : ExtensionPool.GetOrAdd(extension, static value => value);
}
