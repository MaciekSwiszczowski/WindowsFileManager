using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTableData;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileEntryObservableRowStoreTests
{
    [Test]
    public async Task Test_Reset_DeduplicatesByPathAndSortsRows()
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
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt|b.txt");
        await Assert.That(sut.Rows[1].Model!.Size).IsEqualTo(2);
    }

    [Test]
    public async Task Test_AddOrUpdate_ReplacesExistingRowAndKeepsSort()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("b.txt", size: 1), File("a.txt", size: 1)], CreateNameComparer());

        // Act
        var mutation = sut.AddOrUpdate(File("b.txt", size: 5));

        // Assert
        await Assert.That(mutation).IsEqualTo(RowMutation.Replaced(1));
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt|b.txt");
        await Assert.That(sut.Rows[1].Model!.Size).IsEqualTo(5);
    }

    [Test]
    public async Task Test_AddOrUpdate_InsertsNewRowInSortedPosition()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1), File("c.txt", size: 1)], CreateNameComparer());

        // Act
        var mutation = sut.AddOrUpdate(File("b.txt", size: 1));

        // Assert
        await Assert.That(mutation).IsEqualTo(RowMutation.Inserted(1));
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt|b.txt|c.txt");
    }

    [Test]
    public async Task Test_AddOrUpdate_MovesRowWhenSortKeyChanges()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset(
            [File("a.txt", size: 3), File("b.txt", size: 1), File("c.txt", size: 2)],
            CreateSizeComparer());

        // Act
        var mutation = sut.AddOrUpdate(File("a.txt", size: 0));

        // Assert
        await Assert.That(mutation).IsEqualTo(RowMutation.Moved(fromIndex: 2, toIndex: 0));
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt|b.txt|c.txt");
        await Assert.That(sut.Rows[0].Model!.Size).IsEqualTo(0);
    }

    [Test]
    public async Task Test_Remove_RemovesMatchingKeyAndReturnsItsIndex()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        var removeTarget = File("b.txt", size: 1);
        sut.Reset([File("a.txt", size: 1), removeTarget, File("c.txt", size: 1)], CreateNameComparer());

        // Act
        var removedIndex = sut.Remove(removeTarget.GetKey());

        // Assert
        await Assert.That(removedIndex).IsEqualTo(1);
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt|c.txt");
    }

    [Test]
    public async Task Test_Remove_ReturnsNegativeWhenKeyAbsent()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1)], CreateNameComparer());

        // Act
        var removedIndex = sut.Remove(File("missing.txt", size: 1).GetKey());

        // Assert
        await Assert.That(removedIndex).IsEqualTo(-1);
        await Assert.That(GetNames(sut)).IsEqualTo("a.txt");
    }

    [Test]
    public async Task Test_Sort_ReordersRowsUnderNewComparer()
    {
        // Arrange
        using var sut = new FileEntryObservableRowStore();
        sut.Reset([File("a.txt", size: 1), File("b.txt", size: 1), File("c.txt", size: 1)], CreateNameComparer());

        // Act
        sut.Sort(CreateNameComparer(ascending: false));

        // Assert
        await Assert.That(GetNames(sut)).IsEqualTo("c.txt|b.txt|a.txt");
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
