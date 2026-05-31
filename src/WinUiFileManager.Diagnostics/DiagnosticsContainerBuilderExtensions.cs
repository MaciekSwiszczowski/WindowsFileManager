using Autofac;
using WinUiFileManager.Diagnostics.FileOperations;
using WinUiFileManager.Diagnostics.Inspector;

namespace WinUiFileManager.Diagnostics;

public static class DiagnosticsContainerBuilderExtensions
{
    public static ContainerBuilder AddDiagnosticsServices(this ContainerBuilder builder)
    {
        builder.RegisterType<FileOperationRequestHandler>().SingleInstance();
        builder.RegisterType<InspectorCloudDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorIdentityDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorLinksDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorLocksDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorSecurityDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorStreamsDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorThumbnailDiagnosticsHandler>().SingleInstance();
        return builder;
    }
}
