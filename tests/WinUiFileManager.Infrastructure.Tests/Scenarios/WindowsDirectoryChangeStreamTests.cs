using System.Reactive.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Tests.Fixtures;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsDirectoryChangeStreamTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    [Test]
    public async Task Test_Watch_EmitsCreated_ForNewFile()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Created)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        fixture.CreateFile("created.txt");

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        await Assert.That(signalled.Path.DisplayPath).Contains("created.txt");
    }

    [Test]
    public async Task Test_Watch_EmitsDeleted_ForRemovedFile()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var fullPath = fixture.CreateFile("to-delete.txt");
        var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Deleted)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        File.Delete(fullPath);

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        await Assert.That(signalled.Path.DisplayPath).Contains("to-delete.txt");
    }

    [Test]
    public async Task Test_Watch_EmitsRenamed_WithOldAndNewPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var oldFullPath = fixture.CreateFile("old.txt");
        var newFullPath = Path.Combine(fixture.RootPath, "new.txt");
        var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Renamed)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        File.Move(oldFullPath, newFullPath);

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        await Assert.That(signalled.Path.DisplayPath).Contains("new.txt");
        await Assert.That(signalled.OldPath).IsNotNull();
        await Assert.That(signalled.OldPath!.Value.DisplayPath).Contains("old.txt");
    }

    [Test]
    public async Task Test_Watch_EmitsInvalidated_ForMissingDirectory()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var missingPath = NormalizedPath.FromUserInput(
            Path.Combine(fixture.RootPath, "does-not-exist"));
        var sut = CreateStream();
        var ready = new TaskCompletionSource<DirectoryChange>();

        // Act
        using var subscription = sut.Watch(missingPath)
            .Where(c => c.Kind == DirectoryChangeKind.Invalidated)
            .Subscribe(change => ready.TrySetResult(change));

        // Assert
        await ready.Task.WaitAsync(EventTimeout);
    }

    [Test]
    public async Task Test_Watch_StopsEmitting_AfterDispose()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var count = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Created)
            .Subscribe(_ =>
            {
                Interlocked.Increment(ref count);
                ready.TrySetResult();
            }))
        {
            // Act
            fixture.CreateFile("before-dispose.txt");
            await ready.Task.WaitAsync(EventTimeout);
        }

        fixture.CreateFile("after-dispose.txt");
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        // Assert
        await Assert.That(count).IsEqualTo(1);
    }

    private static WindowsDirectoryChangeStream CreateStream()
    {
        return new WindowsDirectoryChangeStream(
            NullLogger<WindowsDirectoryChangeStream>.Instance);
    }
}
