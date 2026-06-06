using R3;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class WindowsDirectoryChangeStreamTests
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Watch_EmitsCreated_ForNewFile()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        using var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Created)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        fixture.CreateFile("created.txt");

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        Assert.Contains("created.txt", signalled.Path);
    }

    [Fact]
    public async Task Watch_EmitsDeleted_ForRemovedFile()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var fullPath = fixture.CreateFile("to-delete.txt");
        using var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Deleted)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        File.Delete(fullPath);

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        Assert.Contains("to-delete.txt", signalled.Path);
    }

    [Fact]
    public async Task Watch_EmitsRenamed_WithOldAndNewPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var oldFullPath = fixture.CreateFile("old.txt");
        var newFullPath = Path.Combine(fixture.RootPath, "new.txt");
        using var sut = CreateStream();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);
        var ready = new TaskCompletionSource<DirectoryChange>();

        using var subscription = sut.Watch(path)
            .Where(c => c.Kind == DirectoryChangeKind.Renamed)
            .Subscribe(change => ready.TrySetResult(change));

        // Act
        new FileInfo(oldFullPath).MoveTo(newFullPath);

        // Assert
        var signalled = await ready.Task.WaitAsync(EventTimeout);
        Assert.Contains("new.txt", signalled.Path);
        Assert.NotNull(signalled.OldPath);
        Assert.Contains("old.txt", signalled.OldPath!);
    }

    [Fact]
    public async Task Watch_StopsEmitting_AfterDispose()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        using var sut = CreateStream();
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
        Assert.Equal(1, count);
    }

    private static WindowsDirectoryChangeStream CreateStream()
    {
        return new WindowsDirectoryChangeStream(
            NullLogger<WindowsDirectoryChangeStream>.Instance);
    }
}
