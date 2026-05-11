using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class PanelNavigationServiceTests
{
    [Test]
    public async Task Test_NavigateUp_UsesStoredCurrentPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var childPath = fixture.CreateDirectory("Child");
        var messenger = new StrongReferenceMessenger();
        using var sut = new PanelNavigationService(messenger);
        FileTableNavigateToPathMessage? received = null;
        messenger.Register<object, FileTableNavigateToPathMessage>(
            this,
            (_, message) => received = message);

        // Act
        messenger.Send(new FileTableNavigateToPathRequestedMessage(
            "Left",
            NormalizedPath.FromUserInput(childPath)));
        messenger.Send(new FileTableNavigateUpRequestedMessage("Left"));

        // Assert
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Identity).IsEqualTo("Left");
        await Assert.That(received.Path.DisplayPath).IsEqualTo(fixture.RootPath);
    }

    [Test]
    public async Task Test_NavigateDown_CombinesStoredPathAndFolderName()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var childPath = fixture.CreateDirectory("Child");
        var messenger = new StrongReferenceMessenger();
        using var sut = new PanelNavigationService(messenger);
        FileTableNavigateToPathMessage? received = null;
        messenger.Register<object, FileTableNavigateToPathMessage>(
            this,
            (_, message) => received = message);

        // Act
        messenger.Send(new FileTableNavigateToPathRequestedMessage(
            "Right",
            NormalizedPath.FromUserInput(fixture.RootPath)));
        messenger.Send(new FileTableNavigateDownRequestedMessage("Right", "Child"));

        // Assert
        await Assert.That(received).IsNotNull();
        await Assert.That(received!.Identity).IsEqualTo("Right");
        await Assert.That(received.Path.DisplayPath).IsEqualTo(childPath);
    }
}
