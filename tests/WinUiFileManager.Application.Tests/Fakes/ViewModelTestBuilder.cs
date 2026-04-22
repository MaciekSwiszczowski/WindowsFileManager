using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure.Scheduling;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class ViewModelTestBuilder
{
    public FakeDialogService DialogService { get; } = new();
    public FakeClipboardService ClipboardService { get; } = new();
    public FakeSettingsRepository SettingsRepository { get; } = new();
    public FakeFavouritesRepository FavouritesRepository { get; } = new();
    public FakeShellService ShellService { get; } = new();
    public IFileOperationService? FileOperationServiceOverride { get; set; }
    public IFileIdentityService? FileIdentityServiceOverride { get; set; }
    public ISchedulerProvider? SchedulerProviderOverride { get; set; }

    public MainShellViewModel Build()
    {
        var pathService = new WindowsPathNormalizationService();
        var volumeInterop = new VolumeInterop();
        var fileOpInterop = new FileOperationInterop();

        var fsService = new WindowsFileSystemService(
            pathService, NullLogger<WindowsFileSystemService>.Instance);
        var changeStream = new WindowsDirectoryChangeStream(
            NullLogger<WindowsDirectoryChangeStream>.Instance);
        var schedulers = SchedulerProviderOverride ?? new RxSchedulerProvider();
        var fileIdentityService = FileIdentityServiceOverride ?? new NtfsFileIdentityService(new FileIdentityInterop());
        var volumePolicy = new NtfsVolumePolicyService(volumeInterop);
        var planner = new WindowsFileOperationPlanner(
            NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = FileOperationServiceOverride ?? new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        var openEntry = new OpenEntryCommandHandler(
            fsService, volumePolicy, ShellService, NullLogger<OpenEntryCommandHandler>.Instance);

        var copyHandler = new CopySelectionCommandHandler(
            planner, operationService, NullLogger<CopySelectionCommandHandler>.Instance);
        var moveHandler = new MoveSelectionCommandHandler(
            planner, operationService, NullLogger<MoveSelectionCommandHandler>.Instance);
        var deleteHandler = new DeleteSelectionCommandHandler(
            planner, operationService, NullLogger<DeleteSelectionCommandHandler>.Instance);
        var createFolderHandler = new CreateFolderCommandHandler(
            planner, operationService, NullLogger<CreateFolderCommandHandler>.Instance);
        var renameHandler = new RenameEntryCommandHandler(
            operationService, NullLogger<RenameEntryCommandHandler>.Instance);
        var copyFullPath = new CopyFullPathCommandHandler(ClipboardService);
        var addFavourite = new AddFavouriteCommandHandler(
            FavouritesRepository, volumePolicy, NullLogger<AddFavouriteCommandHandler>.Instance);
        var removeFavourite = new RemoveFavouriteCommandHandler(
            FavouritesRepository, NullLogger<RemoveFavouriteCommandHandler>.Instance);
        var openFavourite = new OpenFavouriteCommandHandler(
            FavouritesRepository, fsService, NullLogger<OpenFavouriteCommandHandler>.Instance);
        var setParallelExec = new SetParallelExecutionCommandHandler(
            SettingsRepository, NullLogger<SetParallelExecutionCommandHandler>.Instance);
        var persistPaneState = new PersistPaneStateCommandHandler(
            SettingsRepository, NullLogger<PersistPaneStateCommandHandler>.Instance);

        var leftPane = new FilePaneViewModel(
            openEntry, renameHandler, fsService, changeStream, schedulers, volumePolicy, pathService,
            NullLogger<FilePaneViewModel>.Instance);
        var rightPane = new FilePaneViewModel(
            openEntry, renameHandler, fsService, changeStream, schedulers, volumePolicy, pathService,
            NullLogger<FilePaneViewModel>.Instance);
        var inspector = new FileInspectorViewModel(
            fileIdentityService,
            ClipboardService,
            ShellService,
            NullLogger<FileInspectorViewModel>.Instance);

        return new MainShellViewModel(
            SettingsRepository,
            copyHandler,
            moveHandler,
            deleteHandler,
            createFolderHandler,
            renameHandler,
            copyFullPath,
            addFavourite,
            removeFavourite,
            openFavourite,
            setParallelExec,
            persistPaneState,
            DialogService,
            FavouritesRepository,
            schedulers,
            NullLogger<MainShellViewModel>.Instance,
            inspector,
            leftPane,
            rightPane);
    }
}
