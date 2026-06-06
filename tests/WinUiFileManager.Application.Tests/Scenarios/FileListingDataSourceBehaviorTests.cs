using System.Runtime.CompilerServices;

namespace WinUiFileManager.Application.Tests.Scenarios;

/// <summary>
/// End-to-end behavioral tests for <see cref="FileListingDataSource"/> driven over a real temp folder with the
/// real Windows scanner, reader, and directory-change watcher (see <see cref="FileListingEngineHarness"/>).
/// Each test re-creates the watcher, then performs a real filesystem operation and polls until the pipeline's
/// <c>Items</c> reflect it.
/// </summary>
public sealed class FileListingDataSourceBehaviorTests
{
#if DEBUG
    // GC/WeakReference assertions are unreliable in Debug: the JIT keeps locals alive to the end of their
    // method for debugging, so reclaimable objects can still appear rooted. These tests pass in Release.
    private const string MemoryTestSkip =
        "Memory/GC assertions require a Release build (Debug extends local lifetimes). Run: dotnet test -c Release.";
#else
    private const string? MemoryTestSkip = null;
#endif

    [Fact]
    public void Seed_PopulatesItemsFromRealFolder()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        temp.CreateFile("apple.txt");
        temp.CreateFile("banana.txt");
        using var harness = new FileListingEngineHarness();

        // Act
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 2);

        // Assert
        Assert.Equal(["apple.txt", "banana.txt"], harness.SnapshotEntryNames());
    }

    [Fact]
    public void CreatedFile_IsAddedInSortedPosition()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        temp.CreateFile("apple.txt");
        temp.CreateFile("cherry.txt");
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 2);

        // Act
        temp.CreateFile("banana.txt");

        // Assert
        harness.WaitForItems(static rows => NamesAre(rows, "apple.txt", "banana.txt", "cherry.txt"));
    }

    [Fact]
    public void DeletedFile_IsRemoved()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        temp.CreateFile("apple.txt");
        var banana = temp.CreateFile("banana.txt");
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 2);

        // Act
        File.Delete(banana);

        // Assert
        harness.WaitForItems(static rows => NamesAre(rows, "apple.txt"));
    }

    [Fact]
    public void RenamedFile_ReplacesOldNameWithNewInSortedPosition()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        var apple = temp.CreateFile("apple.txt");
        temp.CreateFile("mango.txt");
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 2);

        // Act
        File.Move(apple, Path.Combine(temp.RootPath, "zebra.txt"));

        // Assert
        harness.WaitForItems(static rows => NamesAre(rows, "mango.txt", "zebra.txt"));
    }

    [Fact]
    public void SortRequest_ReordersItems()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        temp.CreateFile("apple.txt");
        temp.CreateFile("banana.txt");
        temp.CreateFile("cherry.txt");
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => NamesAre(rows, "apple.txt", "banana.txt", "cherry.txt"));

        // Act
        harness.RequestSort(SortColumn.Name, ascending: false);

        // Assert
        harness.WaitForItems(static rows => NamesAre(rows, "cherry.txt", "banana.txt", "apple.txt"));
    }

    [Fact]
    public void SizeChange_MovesRowWhenSortingBySize()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        var small = temp.CreateFile("a-small.txt", sizeInBytes: 10);
        temp.CreateFile("z-big.txt", sizeInBytes: 100);
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 2);
        harness.RequestSort(SortColumn.Size, ascending: true);
        harness.WaitForItems(static rows => NamesAre(rows, "a-small.txt", "z-big.txt"));

        // Act
        File.WriteAllBytes(small, new byte[200]);

        // Assert
        harness.WaitForItems(static rows => NamesAre(rows, "z-big.txt", "a-small.txt"));
    }

    [Fact(Skip = MemoryTestSkip)]
    public void Dispose_ReleasesComponentForGarbageCollection()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        temp.CreateFile("apple.txt");

        // Act
        var dataSourceReference = SeedThenDispose(temp.RootPath);
        Collect();

        // Assert
        Assert.False(dataSourceReference.IsAlive, "FileListingDataSource should be unreachable after Dispose.");
    }

    [Fact(Skip = MemoryTestSkip)]
    public void FileUpdate_ReleasesReplacedRowForGarbageCollection()
    {
        // Arrange
        using var temp = new NtfsTempDirectoryFixture();
        var apple = temp.CreateFile("apple.txt", sizeInBytes: 0);
        using var harness = new FileListingEngineHarness();
        harness.Start(temp.RootPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 1);
        var replacedRowReference = CaptureRow(harness, "apple.txt");

        // Act
        File.WriteAllBytes(apple, new byte[64]);
        harness.WaitForItems(static rows => rows.Any(static row => row.Model is { Name: "apple.txt", Size: > 0 }));
        Collect();

        // Assert
        Assert.False(replacedRowReference.IsAlive, "The replaced row should be unreachable after the file update.");
    }

    // The data source is built, exercised, and disposed entirely inside this non-inlined frame so no local
    // root keeps it alive after it returns — only the (weak) reference survives.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SeedThenDispose(string folderPath)
    {
        using var harness = new FileListingEngineHarness();
        harness.Start(folderPath);
        harness.WaitForItems(static rows => RealEntryCount(rows) == 1);
        return new WeakReference(harness.DataSource);
    }

    // Snapshot is local to this non-inlined frame so the only surviving reference to the row is the weak one.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CaptureRow(FileListingEngineHarness harness, string name)
    {
        var row = harness.SnapshotItems().Single(row => row.Model?.Name == name);
        return new WeakReference(row);
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static int RealEntryCount(IReadOnlyList<FileListingRow> rows) =>
        rows.Count(static row => row.Model is not null);

    private static bool NamesAre(IReadOnlyList<FileListingRow> rows, params string[] expected) =>
        rows.Where(static row => row.Model is not null)
            .Select(static row => row.Model!.Name)
            .SequenceEqual(expected, StringComparer.OrdinalIgnoreCase);
}
