using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTableData;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileEntryObservableRowStoreTests
{
    [Fact]
    public void Reset_DeduplicatesByPathAndSortsRows()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();

        // Act
        sut.Reset(
            [
                File("b.txt", size: 1),
                File("a.txt", size: 1),
                File("b.txt", size: 2),
            ],
            CreateNameComparer());

        // Assert
        Assert.Equal("a.txt|b.txt", GetNames(sut));
        Assert.Equal(2, sut.Rows[1].Model!.Size);
    }

    [Fact]
    public void AddOrUpdate_ReplacesExistingRowAndKeepsSort()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("b.txt", size: 1), File("a.txt", size: 1)], CreateNameComparer());

        // Act
        sut.AddOrUpdate(File("b.txt", size: 5));

        // Assert
        Assert.Equal("a.txt|b.txt", GetNames(sut));
        Assert.Equal(5, sut.Rows[1].Model!.Size);
    }

    [Fact]
    public void AddOrUpdate_InsertsNewRowInSortedPosition()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1), File("c.txt", size: 1)], CreateNameComparer());

        // Act
        sut.AddOrUpdate(File("b.txt", size: 1));

        // Assert
        Assert.Equal("a.txt|b.txt|c.txt", GetNames(sut));
    }

    [Fact]
    public void AddOrUpdate_MovesRowWhenSortKeyChanges()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset(
            [File("a.txt", size: 3), File("b.txt", size: 1), File("c.txt", size: 2)],
            CreateSizeComparer());

        // Act
        sut.AddOrUpdate(File("a.txt", size: 0));

        // Assert
        Assert.Equal("a.txt|b.txt|c.txt", GetNames(sut));
        Assert.Equal(0, sut.Rows[0].Model!.Size);
    }

    [Fact]
    public void Remove_RemovesMatchingKey()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        var removeTarget = File("b.txt", size: 1);
        sut.Reset([File("a.txt", size: 1), removeTarget, File("c.txt", size: 1)], CreateNameComparer());

        // Act
        var removed = sut.Remove(removeTarget.GetKey());

        // Assert
        Assert.True(removed);
        Assert.Equal("a.txt|c.txt", GetNames(sut));
    }

    [Fact]
    public void Remove_ReturnsFalseWhenKeyAbsent()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1)], CreateNameComparer());

        // Act
        var removed = sut.Remove(File("missing.txt", size: 1).GetKey());

        // Assert
        Assert.False(removed);
        Assert.Equal("a.txt", GetNames(sut));
    }

    [Fact]
    public void Sort_ReordersRowsUnderNewComparer()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1), File("b.txt", size: 1), File("c.txt", size: 1)], CreateNameComparer());

        // Act
        sut.Sort(CreateNameComparer(ascending: false));

        // Assert
        Assert.Equal("c.txt|b.txt|a.txt", GetNames(sut));
    }

    private static IComparer<SpecFileEntryViewModel> CreateNameComparer(bool ascending = true) =>
        new SpecFileEntryComparer(SortColumn.Name, ascending, FileEntryDisplayStringCache.Shared);

    private static IComparer<SpecFileEntryViewModel> CreateSizeComparer(bool ascending = true) =>
        new SpecFileEntryComparer(SortColumn.Size, ascending, FileEntryDisplayStringCache.Shared);

    private static string GetNames(FileEntryObservableRowStore store) =>
        string.Join("|", store.Rows.Select(static row => row.Model?.Name));

    private static SpecFileEntryViewModel File(string name, long size) =>
        new(new FileSystemEntryModel(
            NormalizedPath.FromUserInput(@"C:\Temp"),
            name,
            Path.GetExtension(name),
            ItemKind.File,
            size,
            DateTime.Today,
            DateTime.Today,
            FileAttributes.Normal));
}
