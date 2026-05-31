using CommunityToolkit.Mvvm.Messaging;
using Autofac;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Persistence;
using WinUiFileManager.Infrastructure.Scheduling;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure;

public static class InfrastructureContainerBuilderExtensions
{
    public static ContainerBuilder AddInfrastructureServices(this ContainerBuilder builder)
    {
        builder.RegisterInstance(StrongReferenceMessenger.Default).As<IMessenger>();

        builder.RegisterType<ShellInterop>().As<IShellInterop>().SingleInstance();
        builder.RegisterType<RestartManagerInterop>().As<IRestartManagerInterop>().SingleInstance();
        builder.RegisterType<CloudFilesInterop>().As<ICloudFilesInterop>().SingleInstance();
        builder.RegisterType<FileSystemMetadataInterop>().As<IFileSystemMetadataInterop>().SingleInstance();
        builder.RegisterType<VolumeInterop>().As<IVolumeInterop>().SingleInstance();

        builder.RegisterType<NtfsVolumePolicyService>().As<INtfsVolumePolicyService>().SingleInstance();
        builder.RegisterType<WindowsPathNormalizationService>().As<IPathNormalizationService>().SingleInstance();
        builder.RegisterType<RxSchedulerProvider>().As<ISchedulerProvider>().SingleInstance();
        builder.RegisterType<WindowsDirectoryChangeStream>().As<IDirectoryChangeStream>().SingleInstance();
        builder.RegisterType<NtfsFileIdentityService>().As<IFileIdentityService>().SingleInstance();
        builder.RegisterType<WindowsShellService>().As<IShellService>().SingleInstance();

        builder.RegisterType<JsonSettingsRepository>().As<ISettingsRepository>().SingleInstance();

        builder.RegisterType<SystemTimeProvider>().As<ITimeProvider>().SingleInstance();
        builder.RegisterType<ActivePanelsService>().AsSelf().As<IActivePanelsService>().SingleInstance();
        builder.RegisterType<RenameService>().SingleInstance();

        return builder;
    }
}
