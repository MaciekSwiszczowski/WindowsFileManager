using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure.Scheduling;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class ViewModelTestBuilder
{
    public FakeClipboardService ClipboardService { get; } = new();
    public FakeSettingsRepository SettingsRepository { get; } = new();
    public FakeFavouritesRepository FavouritesRepository { get; } = new();
    public FakeShellService ShellService { get; } = new();
    public IFileIdentityService? FileIdentityServiceOverride { get; set; }
    public ISchedulerProvider? SchedulerProviderOverride { get; set; }

    public MainShellViewModel Build()
    {
        var fileIdentityService = FileIdentityServiceOverride
            ?? new NtfsFileIdentityService(
                new RestartManagerInterop(),
                new CloudFilesInterop(),
                new FileSystemMetadataInterop());
        var schedulers = SchedulerProviderOverride ?? new RxSchedulerProvider();
        var pathService = new WindowsPathNormalizationService();
        var fsService = new WindowsFileSystemService(
            pathService, NullLogger<WindowsFileSystemService>.Instance);

        var removeFavourite = new RemoveFavouriteCommandHandler(
            FavouritesRepository, NullLogger<RemoveFavouriteCommandHandler>.Instance);
        var openFavourite = new OpenFavouriteCommandHandler(
            FavouritesRepository, fsService, NullLogger<OpenFavouriteCommandHandler>.Instance);
        var setParallelExec = new SetParallelExecutionCommandHandler(
            SettingsRepository, NullLogger<SetParallelExecutionCommandHandler>.Instance);
        var persistPaneState = new PersistPaneStateCommandHandler(
            SettingsRepository, NullLogger<PersistPaneStateCommandHandler>.Instance);

#pragma warning disable IDISP004
        var messenger = new StrongReferenceMessenger();
        return new MainShellViewModel(
            SettingsRepository,
            removeFavourite,
            openFavourite,
            setParallelExec,
            persistPaneState,
            FavouritesRepository,
            NullLogger<MainShellViewModel>.Instance,
            messenger,
            CreateInspectorViewModel(
                fileIdentityService,
                ClipboardService,
                ShellService,
                schedulers,
                messenger),
            new AppInitializationViewModel(new FakeNtfsVolumePolicyService()),
            new PanelsViewModel(new FakeActivePanelsService(), messenger, fsService),
            new CommandButtonsViewModel(messenger));
#pragma warning restore IDISP004
    }

    private static FileInspectorViewModel CreateInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers,
        IMessenger messenger)
    {
        return new FileInspectorViewModel(
            fileIdentityService,
            clipboardService,
            shellService,
            schedulers,
            NullLogger<FileInspectorViewModel>.Instance,
            messenger);
    }
}
