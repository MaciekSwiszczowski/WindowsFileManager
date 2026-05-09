using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Persistence;
using WinUiFileManager.Infrastructure.Scheduling;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);

        services.AddSingleton<IShellInterop, ShellInterop>();
        services.AddSingleton<IRestartManagerInterop, RestartManagerInterop>();
        services.AddSingleton<ICloudFilesInterop, CloudFilesInterop>();
        services.AddSingleton<IFileSystemMetadataInterop, FileSystemMetadataInterop>();
        services.AddSingleton<IVolumeInterop, VolumeInterop>();

        services.AddSingleton<INtfsVolumePolicyService, NtfsVolumePolicyService>();
        services.AddSingleton<IPathNormalizationService, WindowsPathNormalizationService>();
        services.AddSingleton<ISchedulerProvider, RxSchedulerProvider>();
        services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        services.AddSingleton<IDirectoryChangeStream, WindowsDirectoryChangeStream>();
        services.AddSingleton<IFileIdentityService, NtfsFileIdentityService>();
        services.AddSingleton<IShellService, WindowsShellService>();

        services.AddSingleton<IFavouritesRepository, JsonFavouritesRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();

        services.AddSingleton<ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<ActivePanelsService>();
        services.AddSingleton<IActivePanelsService>(static provider => provider.GetRequiredService<ActivePanelsService>());
        services.AddSingleton<RenameService>();

        return services;
    }
}
