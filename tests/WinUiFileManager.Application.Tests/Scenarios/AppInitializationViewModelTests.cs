namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class AppInitializationViewModelTests
{
    [Test]
    public async Task Test_Initialize_CopiesStartupSettingsAndVolumes()
    {
        var fixture = ApplicationAutoFixture.Create();
        var sut = fixture.Create<AppInitializationViewModel>();
        var settings = new AppSettings(inspectorVisible: false);
        var volumes = new[]
        {
            Volume("C", @"C:\"),
            Volume("D", @"D:\"),
        };

        sut.Initialize(settings, volumes);

        await Assert.That(sut.InspectorVisible).IsFalse();
        await Assert.That(sut.AvailableVolumes.Count).IsEqualTo(2);
        await Assert.That(string.Join("|", sut.AvailableVolumes.Select(static volume => volume.DriveLetter))).IsEqualTo("C|D");
    }

    [Test]
    public async Task Test_Initialize_IsSingleUse()
    {
        var fixture = ApplicationAutoFixture.Create();
        var sut = fixture.Create<AppInitializationViewModel>();

        sut.Initialize(new AppSettings(inspectorVisible: false), [Volume("C", @"C:\")]);
        sut.Initialize(new AppSettings(inspectorVisible: true), [Volume("D", @"D:\")]);

        await Assert.That(sut.InspectorVisible).IsFalse();
        await Assert.That(sut.AvailableVolumes.Single().DriveLetter).IsEqualTo("C");
    }

    private static VolumeInfo Volume(string driveLetter, string path) =>
        new(
            driveLetter,
            label: $"{driveLetter} Drive",
            fileSystemName: "NTFS",
            NormalizedPath.FromUserInput(path),
            isNtfs: true);
}
