using System.Diagnostics;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.App.Startup;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Application.Dialogs;
using WinUiFileManager.Application.Messaging;
using WinUiFileManager.Application.Navigation;
using WinUiFileManager.Application.Settings;
using WinUiFileManager.Diagnostics;
using WinUiFileManager.Infrastructure;
using WinUiFileManager.Presentation;
using WinUiFileManager.Presentation.Messaging;
using WinUiFileManager.Presentation.MessageLogging;
using WinUiFileManager.Presentation.Services;
using WinUiFileManager.Presentation.ViewModels;
using UiDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace WinUiFileManager.App.Composition;

/// <summary>
/// Composition root for the application: wires every layer's services into a single Autofac container
/// and exposes it as an <see cref="AutofacServiceProvider"/>. This is the one place that knows about all
/// layers at once (App layer, AGENTS.md §2).
/// </summary>
/// <remarks>
/// The returned provider/container is held for the process lifetime by <see cref="App"/> and is never
/// disposed, so singletons that implement <see cref="IDisposable"/> are effectively process-lifetime
/// (AGENTS.md §5).
/// </remarks>
public static class ServiceConfiguration
{
    /// <summary>
    /// Builds the application container and returns the resolved service provider.
    /// </summary>
    /// <returns>
    /// An <see cref="AutofacServiceProvider"/> wrapping the built container. The caller owns it for the
    /// process lifetime; it is intentionally not disposed on shutdown (see type remarks).
    /// </returns>
    /// <remarks>
    /// Lifetime choices below are deliberate:
    /// <list type="bullet">
    /// <item><description>Stateful, shared collaborators (navigation, command handlers, the message log
    /// store, dialog/clipboard services, the startup pipeline) are <c>SingleInstance</c> so every
    /// consumer observes the same state and the same messenger registrations.</description></item>
    /// <item><description>Dialog view models are <c>InstancePerDependency</c>: each dialog invocation
    /// needs its own short-lived, independently-bound instance.</description></item>
    /// </list>
    /// Registration order matters only in that the per-layer extension methods register the
    /// abstractions these App-level registrations depend on; the explicit registrations here override or
    /// supplement those.
    /// </remarks>
    public static AutofacServiceProvider ConfigureServices()
    {
        var builder = new ContainerBuilder();

        // MS.Extensions logging is registered through the ServiceCollection bridge and then populated
        // into Autofac so ILogger<T> resolves everywhere.
        var services = new ServiceCollection();
        services.AddLogging();
        builder.Populate(services);

        builder.AddInfrastructureServices();
        RegisterAppMessenger(builder);
        builder.AddDiagnosticsServices();
        builder.AddPresentationServices();

        builder.RegisterType<PanelNavigationService>().SingleInstance();

        builder.RegisterType<SetParallelExecutionCommandHandler>().SingleInstance();
        builder.RegisterType<PersistPaneStateCommandHandler>().SingleInstance();

        builder.RegisterType<MessageLogStore>().SingleInstance();

        builder.RegisterType<DialogService>().SingleInstance();
        builder.RegisterType<WinUiClipboardService>().As<IClipboardService>().SingleInstance();

        // Dialog VMs are transient: one per dialog invocation, each independently bound.
        builder.RegisterType<MessageDialogViewModel>().InstancePerDependency();
        builder.RegisterType<RenameDialogViewModel>().InstancePerDependency();

        builder.RegisterType<StartupChain>().SingleInstance();
        builder.RegisterType<StartupChainRunner>().SingleInstance();
        builder.RegisterType<StartupPathResolver>().SingleInstance();

        var container = builder.Build();
        ApplyDevelopmentContainerValidation(container);
        return new AutofacServiceProvider(container);
    }

    /// <summary>
    /// Replaces the Infrastructure non-UI messenger fallback with the application messenger wrapper.
    /// </summary>
    /// <param name="builder">The Autofac container builder being configured by the composition root.</param>
    /// <remarks>
    /// The wrapper lives in Presentation because UI-thread dispatch needs WinUI's <see cref="UiDispatcherQueue"/>.
    /// Registration happens immediately after Infrastructure registration so every App container consumer resolving
    /// <see cref="IMessenger"/> or <see cref="IFileManagerMessenger"/> receives the wrapper.
    /// </remarks>
    private static void RegisterAppMessenger(ContainerBuilder builder)
    {
        var dispatcherQueue = UiDispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("The application messenger must be registered from the UI thread.");

        builder.RegisterInstance(new FileManagerMessenger(StrongReferenceMessenger.Default, dispatcherQueue))
            .As<IFileManagerMessenger>()
            .As<IMessenger>()
            .SingleInstance();
    }

    /// <summary>
    /// Eagerly resolves the root view model in analyzer builds to fail fast on broken DI wiring.
    /// </summary>
    /// <param name="container">The freshly built container to validate.</param>
    /// <remarks>
    /// Compiled in only for the <c>DEBUG_ANALYZERS</c> configuration (AGENTS.md §9) so the resolution
    /// cost and the eager construction of the graph are not paid in normal Debug/Release runs.
    /// </remarks>
    [Conditional("DEBUG_ANALYZERS")]
    private static void ApplyDevelopmentContainerValidation(IContainer container)
    {
        container.Resolve<MainShellViewModel>();
    }
}
