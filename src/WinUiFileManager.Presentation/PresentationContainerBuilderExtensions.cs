using Autofac;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.Threading;
using WinUiFileManager.Presentation.ViewModels.Inspector;
using WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;
using WinUiFileManager.Presentation.ViewModels.Inspector.Search;
using WinUiFileManager.Presentation.ViewModels.Panels;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.Presentation;

/// <summary>
/// Autofac registration extensions for the Presentation layer: registers the file-table services/data
/// sources and all view models with their intended lifetimes. Called from the app composition root
/// the <c>App</c> project owns the container; Presentation only contributes registrations.
/// </summary>
/// <remarks>
/// Lifetime choices matter: the display-string cache is the shared process-lifetime
/// singleton; the row reader/scanner are stateless singletons; the per-folder
/// <see cref="FileEntryTableDataSource"/> and the view models are <c>InstancePerDependency</c> so each
/// pane/window gets its own disposable instance whose teardown is the owner's responsibility.
/// </remarks>
public static class PresentationContainerBuilderExtensions
{
    /// <summary>Registers all Presentation-layer services and view models into the container.</summary>
    public static void AddPresentationServices(this ContainerBuilder builder)
    {
        builder.RegisterPresentationThreading();
        builder.RegisterInstance(FileEntryDisplayStringCache.Shared).SingleInstance();
        builder.RegisterType<FileEntryRowFactory>().SingleInstance();
        // Per-dependency: each folder/pane gets its own disposable data source pipeline.
        builder.RegisterType<FileEntryTableDataSource>().InstancePerDependency();
        builder.RegisterType<WindowsFileEntryRowReader>().As<IFileEntryRowReader>().SingleInstance();
        builder.RegisterType<WindowsFolderEntryScanner>().As<IFolderEntryScanner>().SingleInstance();
        builder.AddPresentationViewModels();
    }

    /// <summary>Registers Presentation-owned UI dispatch services captured from the current WinUI thread.</summary>
    private static void RegisterPresentationThreading(this ContainerBuilder builder)
    {
        var dispatcherQueue = UiDispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("Presentation threading services must be registered from the UI thread.");

        builder.RegisterInstance(new DispatcherQueueUiThreadDispatcher(dispatcherQueue))
            .As<IUiThreadDispatcher>()
            .SingleInstance();
    }

    /// <summary>Registers the Presentation view models. Inspector deferred-field loaders are all
    /// registered against <see cref="IInspectorDeferredFieldLoader"/> so the inspector can resolve them
    /// as a set.</summary>
    private static void AddPresentationViewModels(this ContainerBuilder builder)
    {
        builder.RegisterType<AppInitializationViewModel>().SingleInstance();
        builder.RegisterType<PanelsViewModel>().SingleInstance();
        builder.RegisterType<PanelViewModel>().InstancePerDependency();
        builder.RegisterType<PanelFileEntryDataSourceViewModel>().InstancePerDependency();
        builder.RegisterType<CommandButtonsViewModel>().InstancePerDependency();
        builder.RegisterType<MainShellViewModel>().InstancePerDependency();
        builder.RegisterType<StatusBarViewModel>().InstancePerDependency();

        builder.RegisterType<InspectorInitializationViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorRefreshButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorPropertiesButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorCopyToClipboardButtonViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorSearchViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorAttributeToggleViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorCloudDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorIdentityDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorLinksDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorLocksDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorSecurityDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorStreamsDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorThumbnailDeferredFieldLoader>().As<IInspectorDeferredFieldLoader>().InstancePerDependency();
        builder.RegisterType<InspectorCategoryViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorBasicFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorThumbnailFieldViewModel>().InstancePerDependency();
        builder.RegisterType<InspectorToggleFieldViewModel>().InstancePerDependency();

        builder.RegisterType<SpecFileEntryViewModel>().InstancePerDependency();
    }
}
