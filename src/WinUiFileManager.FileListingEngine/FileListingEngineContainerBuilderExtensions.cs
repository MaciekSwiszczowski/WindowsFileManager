using Autofac;
using WinUiFileManager.FileListingEngine.FileSystem;

namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// Autofac composition extension that wires the file-listing engine's implementations. Called from the
/// Presentation composition root after <see cref="IFileListingStringCache"/> is registered.
/// </summary>
/// <remarks>
/// Keeps DI registration inside this assembly so Presentation depends only on public engine surfaces
/// (<see cref="FileListingDataSource"/>, <see cref="IFileListingRowReader"/>,
/// <see cref="IFolderListingScanner"/>, <see cref="IDirectoryChangeStream"/>) and never needs
/// <c>InternalsVisibleTo</c> access to internal types.
/// </remarks>
public static class FileListingEngineContainerBuilderExtensions
{
    /// <summary>Registers file-listing engine services into <paramref name="builder"/>.</summary>
    /// <param name="builder">The Autofac container builder being configured by the composition root.</param>
    /// <returns>The same <paramref name="builder"/> to allow fluent chaining.</returns>
    public static ContainerBuilder AddFileListingEngineServices(this ContainerBuilder builder)
    {
        builder.RegisterType<FileListingRowFactory>().SingleInstance();
        builder.RegisterType<FileListingDataSource>().InstancePerDependency();
        builder.RegisterType<WindowsFileListingRowReader>().As<IFileListingRowReader>().SingleInstance();
        builder.RegisterType<WindowsFolderListingScanner>().As<IFolderListingScanner>().SingleInstance();
        builder.RegisterType<WindowsDirectoryChangeStream>().As<IDirectoryChangeStream>().SingleInstance();
        // Supplies Func<FileSystemEntryModel, FileListingRow> to FileListingRowFactory.
        builder.RegisterType<FileListingRow>().InstancePerDependency();

        return builder;
    }
}
