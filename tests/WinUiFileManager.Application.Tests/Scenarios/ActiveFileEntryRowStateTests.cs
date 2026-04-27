using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class ActiveFileEntryRowStateTests
{
    [Test]
    public async Task Test_ActivateBodyRow_ShowsIndicatorOnTargetContainer()
    {
        // Arrange
        var item = CreateFileEntry("active.txt");
        var bodyContainer = new object();
        var calls = new List<(object? container, bool show)>();
        var sut = new ActiveFileEntryRowState(
            () => null,
            i => i == item ? bodyContainer : null,
            (container, show) => calls.Add((container, show)));

        // Act
        sut.ActivateBodyRow(item);

        // Assert
        await Assert.That(calls.Count).IsEqualTo(1);
        await Assert.That(calls[0]).IsEqualTo((bodyContainer, true));
    }

    [Test]
    public async Task Test_ActivateBodyRow_HidesPreviousBodyIndicatorBeforeShowingNew()
    {
        // Arrange
        var first = CreateFileEntry("first.txt");
        var second = CreateFileEntry("second.txt");
        var firstContainer = new object();
        var secondContainer = new object();
        var calls = new List<(object? container, bool show)>();
        var sut = new ActiveFileEntryRowState(
            () => null,
            i => i == first ? firstContainer : i == second ? secondContainer : null,
            (container, show) => calls.Add((container, show)));

        // Act
        sut.ActivateBodyRow(first);
        sut.ActivateBodyRow(second);

        // Assert
        await Assert.That(calls.Count).IsEqualTo(3);
        await Assert.That(calls[0]).IsEqualTo((firstContainer, true));
        await Assert.That(calls[1]).IsEqualTo((firstContainer, false));
        await Assert.That(calls[2]).IsEqualTo((secondContainer, true));
    }

    [Test]
    public async Task Test_ActivateParentRow_HidesPreviousBodyIndicatorBeforeShowingParent()
    {
        // Arrange
        var item = CreateFileEntry("active.txt");
        var bodyContainer = new object();
        var parentContainer = new object();
        var calls = new List<(object? container, bool show)>();
        var sut = new ActiveFileEntryRowState(
            () => parentContainer,
            i => i == item ? bodyContainer : null,
            (container, show) => calls.Add((container, show)));

        // Act
        sut.ActivateBodyRow(item);
        sut.ActivateParentRow();

        // Assert
        await Assert.That(calls.Count).IsEqualTo(3);
        await Assert.That(calls[0]).IsEqualTo((bodyContainer, true));
        await Assert.That(calls[1]).IsEqualTo((bodyContainer, false));
        await Assert.That(calls[2]).IsEqualTo((parentContainer, true));
    }

    [Test]
    public async Task Test_ActivateBodyRow_HidesPreviousParentIndicatorBeforeShowingBody()
    {
        // Arrange
        var item = CreateFileEntry("active.txt");
        var bodyContainer = new object();
        var parentContainer = new object();
        var calls = new List<(object? container, bool show)>();
        var sut = new ActiveFileEntryRowState(
            () => parentContainer,
            i => i == item ? bodyContainer : null,
            (container, show) => calls.Add((container, show)));

        // Act
        sut.ActivateParentRow();
        sut.ActivateBodyRow(item);

        // Assert
        await Assert.That(calls.Count).IsEqualTo(3);
        await Assert.That(calls[0]).IsEqualTo((parentContainer, true));
        await Assert.That(calls[1]).IsEqualTo((parentContainer, false));
        await Assert.That(calls[2]).IsEqualTo((bodyContainer, true));
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
