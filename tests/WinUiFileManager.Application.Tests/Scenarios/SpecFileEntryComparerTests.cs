using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class SpecFileEntryComparerTests
{
    [Test]
    public async Task Test_SizeSort_KeepsDirectoriesSortedByName()
    {
        // Arrange
        var comparer = new SpecFileEntryComparer(SortColumn.Size, ascending: false);
        var items = new List<SpecFileEntryViewModel>
        {
            File("small.txt", 1),
            Directory("zeta"),
            File("large.txt", 10),
            Directory("alpha"),
        };

        // Act
        items.Sort(comparer);

        // Assert
        await Assert.That(string.Join("|", items.Select(static item => item.Model?.Name)))
            .IsEqualTo("alpha|zeta|large.txt|small.txt");
    }

    [Test]
    public async Task Test_NameSort_AppliesDirectionInsideDirectoryGroup()
    {
        // Arrange
        var comparer = new SpecFileEntryComparer(SortColumn.Name, ascending: false);
        var items = new List<SpecFileEntryViewModel>
        {
            File("a.txt", 1),
            Directory("alpha"),
            File("z.txt", 10),
            Directory("zeta"),
        };

        // Act
        items.Sort(comparer);

        // Assert
        await Assert.That(string.Join("|", items.Select(static item => item.Model?.Name)))
            .IsEqualTo("zeta|alpha|z.txt|a.txt");
    }

    private static SpecFileEntryViewModel Directory(string name) =>
        new(new FileSystemEntryModel(
            NormalizedPath.FromUserInput(@"C:\Temp"),
            name,
            string.Empty,
            ItemKind.Directory,
            size: 100,
            DateTime.Today,
            DateTime.Today,
            FileAttributes.Directory));

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
