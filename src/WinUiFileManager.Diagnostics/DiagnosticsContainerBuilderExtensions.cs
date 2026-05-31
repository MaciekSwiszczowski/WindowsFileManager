using Autofac;
using WinUiFileManager.Diagnostics.FileOperations;
using WinUiFileManager.Diagnostics.Inspector;

namespace WinUiFileManager.Diagnostics;

public static class DiagnosticsContainerBuilderExtensions
{
    public static ContainerBuilder AddDiagnosticsServices(this ContainerBuilder builder)
    {
        builder.RegisterType<FileOperationRequestHandler>().SingleInstance();
        builder.RegisterType<InspectorStreamsDiagnosticsHandler>().SingleInstance();
        return builder;
    }
}
