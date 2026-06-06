namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class AppInitializationViewModelTests
{
    [Fact]
    public void Initialize_CopiesStartupSettingsAndVolumes()
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

        Assert.False(sut.InspectorVisible);
        Assert.Equal(2, sut.AvailableVolumes.Count);
        Assert.Equal("C|D", string.Join("|", sut.AvailableVolumes.Select(static volume => volume.DriveLetter)));
    }

    [Fact]
    public void Initialize_IsSingleUse()
    {
        var fixture = ApplicationAutoFixture.Create();
        var sut = fixture.Create<AppInitializationViewModel>();

        sut.Initialize(new AppSettings(inspectorVisible: false), [Volume("C", @"C:\")]);
        sut.Initialize(new AppSettings(inspectorVisible: true), [Volume("D", @"D:\")]);

        Assert.False(sut.InspectorVisible);
        Assert.Equal("C", sut.AvailableVolumes.Single().DriveLetter);
    }

    private static VolumeInfo Volume(string driveLetter, string path) =>
        new(
            driveLetter,
            label: $"{driveLetter} Drive",
            fileSystemName: "NTFS",
            NormalizedPath.FromUserInput(path),
            isNtfs: true);
}
