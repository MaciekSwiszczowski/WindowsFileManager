using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ActiveFileEntryRowStateTests
{
    private static ActiveFileEntryRowState CreateSut()
    {
        var parentRow = SpecFileEntryViewModel.CreateParentEntry();
        return new ActiveFileEntryRowState(
            parentRow,
            () => null,
            _ => null,
            (_, _) => { });
    }

    [Test]
    public async Task Test_ActivateBodyRow_ReplacesPreviousActiveRow()
    {
        // Arrange
        var first = CreateFileEntry("first.txt");
        var second = CreateFileEntry("second.txt");
        var sut = CreateSut();

        // Act
        sut.ActivateBodyRow(first);
        sut.ActivateBodyRow(second);

        // Assert
        await Assert.That(sut.IsBodyRowActive(first)).IsFalse();
        await Assert.That(sut.IsBodyRowActive(second)).IsTrue();
        await Assert.That(sut.IsParentRowActive).IsFalse();
    }

    [Test]
    public async Task Test_FocusLoss_HidesActiveRowAndFocusGainRestoresIt()
    {
        // Arrange
        var item = CreateFileEntry("active.txt");
        var sut = CreateSut();

        // Act
        sut.ActivateBodyRow(item);
        sut.HideIndicator();

        // Assert
        await Assert.That(sut.IsBodyRowActive(item)).IsFalse();

        // Act
        sut.ShowIndicatorIfActiveRowExists(parentRowExists: false, [item]);

        // Assert
        await Assert.That(sut.IsBodyRowActive(item)).IsTrue();
    }

    [Test]
    public async Task Test_ValidateRows_ClearsRemovedBodyInstance()
    {
        // Arrange
        var item = CreateFileEntry("active.txt");
        var replacement = CreateFileEntry("active.txt");
        var sut = CreateSut();

        // Act
        sut.ActivateBodyRow(item);
        sut.ValidateActiveRow(parentRowExists: false, [replacement]);

        // Assert
        await Assert.That(sut.IsBodyRowActive(item)).IsFalse();
        await Assert.That(sut.IsBodyRowActive(replacement)).IsFalse();
    }

    [Test]
    public async Task Test_ParentRow_CanBeActiveAndClearsWhenParentRowIsRemoved()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.ActivateParentRow();

        // Assert
        await Assert.That(sut.IsParentRowActive).IsTrue();

        // Act
        sut.ValidateActiveRow(parentRowExists: false, []);

        // Assert
        await Assert.That(sut.IsParentRowActive).IsFalse();
    }

    private static SpecFileEntryViewModel CreateFileEntry(string name)
    {
        var model = new FileSystemEntryModel(
            NormalizedPath.FromUserInput("C:\\" + name),
            name,
            ".txt",
            ItemKind.File,
            100,
            DateTime.UtcNow,
            DateTime.UtcNow,
            FileAttributes.Normal);

        return new SpecFileEntryViewModel(model);
    }
}
