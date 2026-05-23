using Microsoft.Extensions.DependencyInjection;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileEntryTableDataServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileEntryDataReader, WindowsFileEntryDataReader>();
        return services;
    }
}
