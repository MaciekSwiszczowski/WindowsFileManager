using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.ViewModels;
using WinUiFileManager.Domain.Enums;
using TUnit.Core;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileEntryComparerTests
{
    [Test]
    public async Task Test_NameSort_Ascending_IgnoresCase()
    {
        var file1 = CreateFileEntry("a.txt");
        var file2 = CreateFileEntry("B.txt");

        var sut = new FileEntryComparer(SortColumn.Name, true);

        await Assert.That(sut.Compare(file1, file2)).IsLessThan(0);
        await Assert.That(sut.Compare(file2, file1)).IsGreaterThan(0);
    }

    [Test]
    public async Task Test_NameSort_Descending_ReversesOrder()
    {
        var file1 = CreateFileEntry("a.txt");
        var file2 = CreateFileEntry("b.txt");

        var sut = new FileEntryComparer(SortColumn.Name, false);

        await Assert.That(sut.Compare(file1, file2)).IsGreaterThan(0);
        await Assert.That(sut.Compare(file2, file1)).IsLessThan(0);
    }

    [Test]
    public async Task Test_DirectoriesBeforeFiles()
    {
        // Arrange
        var file = CreateFileEntry("a.txt");
        var dir = CreateDirEntry("z_dir");

        var sut = new FileEntryComparer(SortColumn.Name, true);

        // Act & Assert
        await Assert.That(sut.Compare(dir, file)).IsLessThan(0);
        await Assert.That(sut.Compare(file, dir)).IsGreaterThan(0);
    }

    private static FileEntryViewModel CreateFileEntry(string name)
    {
        var model = new FileSystemEntryModel(
            NormalizedPath.FromUserInput("C:\\" + name),
            name,
            ".txt",
            ItemKind.File,
            100,
            DateTime.UtcNow,
            DateTime.UtcNow,
            System.IO.FileAttributes.Normal);
        return new FileEntryViewModel(model);
    }

    private static FileEntryViewModel CreateDirEntry(string name)
    {
        var model = new FileSystemEntryModel(
            NormalizedPath.FromUserInput("C:\\" + name),
            name,
            "",
            ItemKind.Directory,
            0,
            DateTime.UtcNow,
            DateTime.UtcNow,
            System.IO.FileAttributes.Directory);
        return new FileEntryViewModel(model);
    }
}

