using Autofac;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.FileEntryTableData;

public static class ContainerBuilderExtensions
{
    public static ContainerBuilder AddFileEntryTableDataServices(this ContainerBuilder builder)
    {
        builder.RegisterInstance(FileEntryDisplayStringCache.Shared).SingleInstance();
        builder.RegisterType<FileEntryRowFactory>().SingleInstance();
        builder.RegisterType<WindowsFileEntryRowReader>().As<IFileEntryRowReader>().SingleInstance();
        builder.RegisterType<WindowsFolderEntryScanner>().As<IFolderEntryScanner>().SingleInstance();
        return builder;
    }
}
