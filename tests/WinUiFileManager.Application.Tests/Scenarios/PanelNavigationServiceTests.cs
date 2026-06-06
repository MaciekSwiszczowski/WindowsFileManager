using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Messages.RequestMessages.Navigation;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class PanelNavigationServiceTests
{
    [Fact]
    public void NavigateUp_UsesStoredCurrentPath()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var childPath = fixture.CreateDirectory("Child");
        var messenger = new StrongReferenceMessenger();
        using var sut = new PanelNavigationService(messenger);
        sut.Initialize();
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
        Assert.NotNull(received);
        Assert.Equal("Left", received!.Identity);
        Assert.Equal(fixture.RootPath, received.Path.DisplayPath);
    }

    [Fact]
    public void NavigateDown_CombinesStoredPathAndFolderName()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var childPath = fixture.CreateDirectory("Child");
        var messenger = new StrongReferenceMessenger();
        using var sut = new PanelNavigationService(messenger);
        sut.Initialize();
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
        Assert.NotNull(received);
        Assert.Equal("Right", received!.Identity);
        Assert.Equal(childPath, received.Path.DisplayPath);
    }
}
