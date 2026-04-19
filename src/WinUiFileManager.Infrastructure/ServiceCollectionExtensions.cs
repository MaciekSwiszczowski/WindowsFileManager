using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Infrastructure.Execution;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Logging;
using WinUiFileManager.Infrastructure.Persistence;
using WinUiFileManager.Infrastructure.Planning;
using WinUiFileManager.Infrastructure.Scheduling;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IVolumeInterop, VolumeInterop>();
        services.AddSingleton<IFileIdentityInterop, FileIdentityInterop>();
        services.AddSingleton<IFileOperationInterop, FileOperationInterop>();

        services.AddSingleton<INtfsVolumePolicyService, NtfsVolumePolicyService>();
        services.AddSingleton<IPathNormalizationService, WindowsPathNormalizationService>();
        services.AddSingleton<ISchedulerProvider, RxSchedulerProvider>();
        services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        services.AddSingleton<IDirectoryChangeStream, WindowsDirectoryChangeStream>();
        services.AddSingleton<IFileIdentityService, NtfsFileIdentityService>();
        services.AddSingleton<IFileOperationService, WindowsFileOperationService>();
        services.AddSingleton<IFileOperationPlanner, WindowsFileOperationPlanner>();
        services.AddSingleton<IShellService, WindowsShellService>();

        services.AddSingleton<IFavouritesRepository, JsonFavouritesRepository>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();

        services.AddSingleton<Application.Abstractions.ITimeProvider, SystemTimeProvider>();
        services.AddSingleton<StructuredOperationLogger>();

        return services;
    }
}
