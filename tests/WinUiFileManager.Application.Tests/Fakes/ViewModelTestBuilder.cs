using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Application.Favourites;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Presentation.ViewModels.Inspector;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class ViewModelTestBuilder
{
    public FakeClipboardService ClipboardService { get; } = new();
    public FakeSettingsRepository SettingsRepository { get; } = new();
    public FakeFavouritesRepository FavouritesRepository { get; } = new();
    public FakeShellService ShellService { get; } = new();

    public MainShellViewModel Build()
    {
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
        var activePanels = new FakeActivePanelsService();
        return new MainShellViewModel(
            SettingsRepository,
            removeFavourite,
            openFavourite,
            setParallelExec,
            persistPaneState,
            FavouritesRepository,
            NullLogger<MainShellViewModel>.Instance,
            messenger,
            new InspectorViewModel(),
            new AppInitializationViewModel(new FakeNtfsVolumePolicyService()),
            new PanelsViewModel(activePanels, messenger, fsService),
            new CommandButtonsViewModel(messenger));
#pragma warning restore IDISP004
    }
}
