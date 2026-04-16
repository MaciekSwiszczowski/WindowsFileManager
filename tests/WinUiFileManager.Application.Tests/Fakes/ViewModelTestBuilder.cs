using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Properties;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class ViewModelTestBuilder
{
    public FakeDialogService DialogService { get; } = new();
    public FakeClipboardService ClipboardService { get; } = new();
    public FakeSettingsRepository SettingsRepository { get; } = new();
    public FakeFavouritesRepository FavouritesRepository { get; } = new();
    public FakeShellService ShellService { get; } = new();

    public MainShellViewModel Build()
    {
        var pathService = new WindowsPathNormalizationService();
        var fileIdInterop = new FileIdentityInterop();
        var volumeInterop = new VolumeInterop();
        var fileOpInterop = new FileOperationInterop();

        var fsService = new WindowsFileSystemService(
            pathService, fileIdInterop, NullLogger<WindowsFileSystemService>.Instance);
        var volumePolicy = new NtfsVolumePolicyService(volumeInterop);
        var planner = new WindowsFileOperationPlanner(
            NullLogger<WindowsFileOperationPlanner>.Instance);
        var operationService = new WindowsFileOperationService(
            fileOpInterop, NullLogger<WindowsFileOperationService>.Instance);

        var openEntry = new OpenEntryCommandHandler(
            fsService, volumePolicy, ShellService, NullLogger<OpenEntryCommandHandler>.Instance);
        var navigateUp = new NavigateUpCommandHandler(
            fsService, NullLogger<NavigateUpCommandHandler>.Instance);
        var goToPath = new GoToPathCommandHandler(
            fsService, volumePolicy, pathService, NullLogger<GoToPathCommandHandler>.Instance);
        var refreshPane = new RefreshPaneCommandHandler(
            fsService, NullLogger<RefreshPaneCommandHandler>.Instance);

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
        var showProperties = new ShowPropertiesCommandHandler(DialogService);
        var setParallelExec = new SetParallelExecutionCommandHandler(
            SettingsRepository, NullLogger<SetParallelExecutionCommandHandler>.Instance);
        var persistPaneState = new PersistPaneStateCommandHandler(
            SettingsRepository, NullLogger<PersistPaneStateCommandHandler>.Instance);

        var leftPane = new FilePaneViewModel(
            openEntry, navigateUp, goToPath, refreshPane, volumePolicy,
            NullLogger<FilePaneViewModel>.Instance);
        var rightPane = new FilePaneViewModel(
            openEntry, navigateUp, goToPath, refreshPane, volumePolicy,
            NullLogger<FilePaneViewModel>.Instance);

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
            showProperties,
            setParallelExec,
            persistPaneState,
            DialogService,
            FavouritesRepository,
            NullLogger<MainShellViewModel>.Instance,
            leftPane,
            rightPane);
    }
}
