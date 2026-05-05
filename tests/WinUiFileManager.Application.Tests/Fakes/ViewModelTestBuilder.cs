using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Infrastructure.Scheduling;
using Microsoft.Extensions.Logging;
using WinUiFileManager.Presentation.Services;
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
#pragma warning disable IDISP001 // The test builder intentionally shares one stateless stream instance across both returned pane view-models.
        var changeStream = new WindowsDirectoryChangeStream(
            NullLogger<WindowsDirectoryChangeStream>.Instance);
#pragma warning restore IDISP001
        var schedulers = SchedulerProviderOverride ?? new RxSchedulerProvider();
        var fileIdentityService = FileIdentityServiceOverride
            ?? new NtfsFileIdentityService(
                new RestartManagerInterop(),
                new CloudFilesInterop());
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

#pragma warning disable IDISP004 // Ownership of the created child view-models transfers to MainShellViewModel.
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
            NullLogger<MainShellViewModel>.Instance,
            CreateInspectorViewModel(
                fileIdentityService,
                ClipboardService,
                ShellService,
                schedulers),
            CreatePaneViewModel(
                openEntry, renameHandler, fsService, changeStream, schedulers, volumePolicy, pathService,
                NullLogger<FilePaneViewModel>.Instance),
            CreatePaneViewModel(
                openEntry, renameHandler, fsService, changeStream, schedulers, volumePolicy, pathService,
                NullLogger<FilePaneViewModel>.Instance));
#pragma warning restore IDISP004
    }

    private static FilePaneViewModel CreatePaneViewModel(
        OpenEntryCommandHandler openEntry,
        RenameEntryCommandHandler renameHandler,
        WindowsFileSystemService fsService,
        WindowsDirectoryChangeStream changeStream,
        ISchedulerProvider schedulers,
        NtfsVolumePolicyService volumePolicy,
        WindowsPathNormalizationService pathService,
        ILogger<FilePaneViewModel> logger)
    {
        return new FilePaneViewModel(
            openEntry,
            renameHandler,
            fsService,
            changeStream,
            schedulers,
            volumePolicy,
            pathService,
            logger);
    }

    private static FileInspectorViewModel CreateInspectorViewModel(
        IFileIdentityService fileIdentityService,
        IClipboardService clipboardService,
        IShellService shellService,
        ISchedulerProvider schedulers)
    {
        return new FileInspectorViewModel(
            fileIdentityService,
            clipboardService,
            shellService,
            schedulers,
            NullLogger<FileInspectorViewModel>.Instance);
    }
}
