using CommunityToolkit.Mvvm.Messaging;
using Autofac;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Infrastructure.Persistence;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure;

/// <summary>
/// Autofac composition extension that wires the Infrastructure layer's implementations to their
/// <c>Application</c>-layer abstractions, plus the Interop adapters they depend on. Called from the
/// <c>App</c> composition root. Keeps DI registration out of the App project so Infrastructure owns the
/// knowledge of which concrete type satisfies each contract.
/// </summary>
/// <remarks>
/// Lifetime: everything here is registered <c>SingleInstance</c> (process-lifetime singletons). Per AGENTS.md §5,
/// singletons that implement <see cref="System.IDisposable"/> (e.g. <see cref="ActivePanelsService"/>,
/// <see cref="RenameService"/>) are only released if the container
/// itself is disposed on shutdown. Non-UI containers get the shared <see cref="StrongReferenceMessenger.Default"/>
/// as their <see cref="IMessenger"/> fallback; the App composition root overrides that registration with the
/// Presentation messenger wrapper so UI-thread dispatch stays out of the Infrastructure layer. Both registrations
/// use strong references, so every recipient registered against the messenger roots itself until it calls
/// <c>UnregisterAll</c> (see §4) — missing cleanup is a leak.
/// </remarks>
public static class InfrastructureContainerBuilderExtensions
{
    /// <summary>Registers all Infrastructure services and Interop adapters into <paramref name="builder"/>.</summary>
    /// <param name="builder">The Autofac container builder being configured by the composition root.</param>
    /// <returns>The same <paramref name="builder"/> to allow fluent chaining.</returns>
    public static ContainerBuilder AddInfrastructureServices(this ContainerBuilder builder)
    {
        // Non-UI fallback. The App composition root replaces IMessenger with the DispatcherQueue-aware wrapper.
        builder.RegisterInstance(StrongReferenceMessenger.Default).As<IMessenger>();

        // Interop adapters: the only types allowed to touch Windows.Win32.* directly.
        builder.RegisterType<ShellInterop>().As<IShellInterop>().SingleInstance();
        builder.RegisterType<RestartManagerInterop>().As<IRestartManagerInterop>().SingleInstance();
        builder.RegisterType<FileLockProbeInterop>().SingleInstance();
        builder.RegisterType<CloudFilesInterop>().As<ICloudFilesInterop>().SingleInstance();
        builder.RegisterType<SyncRootRegistryReader>().As<ISyncRootRegistryReader>().SingleInstance();
        builder.RegisterType<FileDeletionInterop>().As<IFileDeletionInterop>().SingleInstance();
        // FileSystemMetadataInterop satisfies two segregated interfaces from one shared instance.
        builder.RegisterType<FileSystemMetadataInterop>()
            .As<IFileSystemMetadataInterop>()
            .As<IAlternateDataStreamInterop>()
            .SingleInstance();
        builder.RegisterType<VolumeInterop>().As<IVolumeInterop>().SingleInstance();

        builder.RegisterType<NtfsVolumePolicyService>().As<INtfsVolumePolicyService>().SingleInstance();
        builder.RegisterType<WindowsPathNormalizationService>().As<IPathNormalizationService>().SingleInstance();
        builder.RegisterType<WindowsShellService>().As<IShellService>().SingleInstance();

        builder.RegisterType<JsonSettingsRepository>().As<ISettingsRepository>().SingleInstance();

        builder.RegisterInstance(TimeProvider.System).SingleInstance();
        // ActivePanelsService is exposed both AsSelf and via its interface so the composition root can call its
        // Initialize() (which registers messenger handlers) on the same instance the interface consumers receive.
        builder.RegisterType<ActivePanelsService>().AsSelf().As<IActivePanelsService>().SingleInstance();
        // RenameService is registered concretely (no interface): it is a self-contained message handler the root
        // resolves and Initialize()s.
        builder.RegisterType<RenameService>().SingleInstance();

        return builder;
    }
}
