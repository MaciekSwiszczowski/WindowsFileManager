using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Reactive.Testing;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;
using WinUiFileManager.Presentation.ViewModels.Panels;
using WinUiFileManager.Presentation.FileEntryTableData;
using WinUiFileManager.Presentation.Services;

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
        var displayStringCache = FileEntryDisplayStringCache.Shared;
        var appInitialization = new AppInitializationViewModel();
        FileEntryTableDataSource.Factory dataSourceFactory = (identity, folderPath) =>
            new FileEntryTableDataSource(
                identity,
                folderPath,
                schedulerProvider,
                folderEntryScanner,
                fileEntryRowReader,
                directoryChangeStream,
                messenger,
                displayStringCache);
        PanelFileEntryDataSourceViewModel.Factory fileEntriesFactory = identity =>
            new PanelFileEntryDataSourceViewModel(
                identity,
                messenger,
                dataSourceFactory,
                schedulerProvider);
        PanelViewModel.Factory panelFactory = identity =>
            new PanelViewModel(identity, messenger, appInitialization, fileEntriesFactory);
        var inspectorInitialization = new InspectorInitializationViewModel(
            activePanels,
            schedulerProvider,
            messenger,
            category => new InspectorCategoryViewModel(category),
            (category, key, tooltip, value) => new InspectorBasicFieldViewModel(category, key, tooltip, value),
            (category, key, tooltip, value) => new InspectorThumbnailFieldViewModel(category, key, tooltip, value),
            (category, key, tooltip, value) => new InspectorToggleFieldViewModel(category, key, tooltip, value));
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
                inspectorAttributes,
                [new InspectorStreamsDeferredFieldLoader(messenger)],
                displayStringCache),
            appInitialization,
            new PanelsViewModel(activePanels, messenger, panelFactory),
            new CommandButtonsViewModel(messenger));
#pragma warning restore IDISP004
    }
}
