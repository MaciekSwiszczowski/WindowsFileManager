using WinUiFileManager.Application.Settings;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class AppInitializationViewModelTests
{
    [Test]
    public async Task Test_InitializeAsync_RestoresExistingSavedPanePaths()
    {
        var leftPath = Directory.CreateTempSubdirectory("wfm-left-");
        var rightPath = Directory.CreateTempSubdirectory("wfm-right-");
        try
        {
            var sut = new AppInitializationViewModel(new FakeNtfsVolumePolicyService());
            var settings = new AppSettings(
                lastLeftPanePath: NormalizedPath.FromUserInput(leftPath.FullName),
                lastRightPanePath: NormalizedPath.FromUserInput(rightPath.FullName));

            await sut.InitializeAsync(settings, CancellationToken.None);

            await Assert.That(sut.LeftInitialPath).IsEqualTo(leftPath.FullName);
            await Assert.That(sut.RightInitialPath).IsEqualTo(rightPath.FullName);
        }
        finally
        {
            leftPath.Delete(recursive: true);
            rightPath.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Test_InitializeAsync_FallsBackToExistingParentForMissingSavedPanePath()
    {
        var root = Directory.CreateTempSubdirectory("wfm-parent-");
        try
        {
            var sut = new AppInitializationViewModel(new FakeNtfsVolumePolicyService());
            var settings = new AppSettings(
                lastLeftPanePath: NormalizedPath.FromUserInput(Path.Combine(root.FullName, "missing", "nested")));

            await sut.InitializeAsync(settings, CancellationToken.None);

            await Assert.That(sut.LeftInitialPath).IsEqualTo(root.FullName);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Test_InitializeAsync_UsesFirstAvailableRootWhenNoSavedPanePath()
    {
        var sut = new AppInitializationViewModel(new FakeNtfsVolumePolicyService());

        await sut.InitializeAsync(new AppSettings(), CancellationToken.None);

        await Assert.That(sut.LeftInitialPath).IsEqualTo(@"C:\");
        await Assert.That(sut.RightInitialPath).IsEqualTo(@"C:\");
    }
}
