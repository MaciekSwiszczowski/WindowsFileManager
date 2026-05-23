using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Reactive.Testing;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;

namespace WinUiFileManager.Application.Tests.Fakes;

public sealed class ViewModelTestBuilder
{
    public FakeClipboardService ClipboardService { get; } = new();
    public FakeSettingsRepository SettingsRepository { get; } = new();
    public FakeShellService ShellService { get; } = new();

    public MainShellViewModel Build()
    {
        var folderEntryScanner = new FakeFolderEntryScanner();
        var fileEntryRowReader = new FakeFileEntryRowReader();
        var directoryChangeStream = new FakeDirectoryChangeStream();
        var setParallelExec = new SetParallelExecutionCommandHandler(
            SettingsRepository, NullLogger<SetParallelExecutionCommandHandler>.Instance);
        var persistPaneState = new PersistPaneStateCommandHandler(
            SettingsRepository, NullLogger<PersistPaneStateCommandHandler>.Instance);

#pragma warning disable IDISP004
        var messenger = new StrongReferenceMessenger();
        var activePanels = new FakeActivePanelsService();
        var schedulerProvider = new TestSchedulerProvider(new TestScheduler());
        var inspectorInitialization = new InspectorInitializationViewModel(
            activePanels,
            schedulerProvider,
            messenger);
        var inspectorRefresh = new InspectorRefreshButtonViewModel(messenger);
        var inspectorProperties = new InspectorPropertiesButtonViewModel(ShellService);
        var inspectorCopy = new InspectorCopyToClipboardButtonViewModel(ClipboardService);
        var inspectorSearch = new InspectorSearchViewModel();
        var inspectorAttributes = new InspectorAttributeToggleViewModel(messenger);

        return new MainShellViewModel(
            SettingsRepository,
            setParallelExec,
            persistPaneState,
            NullLogger<MainShellViewModel>.Instance,
            messenger,
            new InspectorViewModel(
                inspectorInitialization,
                messenger,
                activePanels,
                inspectorRefresh,
                inspectorProperties,
                inspectorCopy,
                inspectorSearch,
                inspectorAttributes),
            new AppInitializationViewModel(new FakeNtfsVolumePolicyService()),
            new PanelsViewModel(activePanels, messenger, folderEntryScanner, fileEntryRowReader, directoryChangeStream),
            new CommandButtonsViewModel(messenger));
#pragma warning restore IDISP004
    }
}
