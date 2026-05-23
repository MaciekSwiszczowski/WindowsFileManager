using Microsoft.Extensions.DependencyInjection;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFileEntryTableDataServices(this IServiceCollection services)
    {
        services.AddSingleton<FileEntryRowFactory>();
        services.AddSingleton<IFileEntryRowReader, WindowsFileEntryRowReader>();
        services.AddSingleton<IFolderEntryScanner, WindowsFolderEntryScanner>();
        return services;
    }
}
