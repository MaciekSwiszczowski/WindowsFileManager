using Autofac;
using WinUiFileManager.Diagnostics.FileOperations;

namespace WinUiFileManager.Diagnostics;

public static class ContainerBuilderExtensions
{
    public static ContainerBuilder AddDiagnosticsServices(this ContainerBuilder builder)
    {
        builder.RegisterType<FileOperationRequestHandler>().SingleInstance();
        return builder;
    }
}
