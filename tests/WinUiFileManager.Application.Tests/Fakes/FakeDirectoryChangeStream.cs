using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IDirectoryChangeStream"/> that lets tests push
/// <see cref="DirectoryChange"/> events for a path through a shared subject.
/// </summary>
public sealed class FakeDirectoryChangeStream : IDirectoryChangeStream
{
    private readonly ConcurrentDictionary<string, Subject<DirectoryChange>> _subjects = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public IObservable<DirectoryChange> Watch(NormalizedPath path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subject = _subjects.GetOrAdd(
            path.DisplayPath,
            static _ => new Subject<DirectoryChange>());

        return subject.AsObservable();
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
