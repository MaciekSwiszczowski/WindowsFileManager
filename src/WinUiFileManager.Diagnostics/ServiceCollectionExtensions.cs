using Microsoft.Extensions.DependencyInjection;
using WinUiFileManager.Diagnostics.FileOperations;

namespace WinUiFileManager.Diagnostics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnosticsServices(this IServiceCollection services)
    {
        services.AddSingleton<FileOperationRequestHandler>();
        return services;
    }
}
