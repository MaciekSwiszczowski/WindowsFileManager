using Autofac;
using WinUiFileManager.Application.Diagnostics;
using WinUiFileManager.Application.Messages.RequestMessages.Inspector;
using WinUiFileManager.Diagnostics.FileOperations;
using WinUiFileManager.Diagnostics.Inspector;
using WinUiFileManager.Diagnostics.Inspector.Handlers;

namespace WinUiFileManager.Diagnostics;

/// <summary>
/// Autofac registration module for the Diagnostics layer: registers the file-operation and inspector
/// request handlers that answer diagnostics messages (AGENTS.md §2).
/// </summary>
public static class DiagnosticsContainerBuilderExtensions
{
    /// <summary>
    /// Registers every diagnostics request handler as a singleton.
    /// </summary>
    /// <param name="builder">The container builder to register into.</param>
    /// <returns>The same <paramref name="builder"/> for fluent chaining.</returns>
    /// <remarks>
    /// All handlers are <c>SingleInstance</c>: each registers itself once with the app-wide messenger in
    /// its <c>Initialize()</c> method and must remain the single live recipient for its message type. The
    /// startup chain resolves these singletons and calls <c>Initialize()</c> on them exactly once.
    /// Because the container is never disposed (AGENTS.md §5), their <see cref="System.IDisposable.Dispose"/>
    /// cleanup is effectively never invoked — acceptable here since they live for the whole process.
    /// </remarks>
    public static ContainerBuilder AddDiagnosticsServices(this ContainerBuilder builder)
    {
        RegisterInspectorResponseFactories(builder);
        builder.RegisterType<ForceDeleteDirectoryHelper>().SingleInstance();
        builder.RegisterType<FileOperationRequestHandler>().SingleInstance();
        builder.RegisterType<StorageProviderSyncRootCache>().SingleInstance();
        builder.RegisterType<InspectorCloudDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorIdentityDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorLinksDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorLocksDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorSecurityDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorStreamsDiagnosticsHandler>().SingleInstance();
        builder.RegisterType<InspectorThumbnailDiagnosticsHandler>().SingleInstance();
        return builder;
    }

    /// <summary>Registers factories that create inspector response messages from loaded diagnostics payloads.</summary>
    private static void RegisterInspectorResponseFactories(ContainerBuilder builder)
    {
        builder.RegisterInstance<Func<FileCloudDiagnosticsDetails, InspectorCloudDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorCloudDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<InspectorIdentityDiagnosticsDetails, InspectorIdentityDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorIdentityDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<FileLinkDiagnosticsDetails, InspectorLinksDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorLinksDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<FileLockDiagnostics, InspectorLocksDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorLocksDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<FileSecurityDiagnosticsDetails, InspectorSecurityDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorSecurityDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<FileStreamDiagnosticsDetails, InspectorStreamsDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorStreamsDiagnosticsResponseMessage(diagnostics));
        builder.RegisterInstance<Func<FileThumbnailDiagnosticsDetails, InspectorThumbnailDiagnosticsResponseMessage>>(
            static diagnostics => new InspectorThumbnailDiagnosticsResponseMessage(diagnostics));
    }
}
