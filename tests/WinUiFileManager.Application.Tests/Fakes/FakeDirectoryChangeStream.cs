using System.Collections.Concurrent;
using R3;
using WinUiFileManager.Application.FileEntries;
using WinUiFileManager.FileListingEngine;

namespace WinUiFileManager.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IDirectoryChangeStream"/> that lets tests push
/// <see cref="DirectoryChange"/> events for a path through a shared subject.
/// </summary>
public sealed class FakeDirectoryChangeStream : IDirectoryChangeStream
{
    private readonly ConcurrentDictionary<string, Subject<DirectoryChange>> _subjects = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public Observable<DirectoryChange> Watch(NormalizedPath path)
    {
        if (_disposed)
        {
            return Observable.Empty<DirectoryChange>();
        }

        var subject = _subjects.GetOrAdd(
            path.DisplayPath,
            static _ => new Subject<DirectoryChange>());

        return subject;
    }

    public void Push(NormalizedPath watchedPath, DirectoryChange change)
    {
        if (_subjects.TryGetValue(watchedPath.DisplayPath, out var subject))
        {
            subject.OnNext(change);
        }
    }

    public void Push(string watchedPath, DirectoryChange change)
    {
        Push(NormalizedPath.FromUserInput(watchedPath), change);
    }

    public bool IsBeingWatched(NormalizedPath watchedPath) =>
        _subjects.ContainsKey(watchedPath.DisplayPath);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var subject in _subjects.Values)
        {
            subject.Dispose();
        }

        _subjects.Clear();
    }
}
