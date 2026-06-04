namespace WinUiFileManager.Benchmarks;

/// <summary>
/// Removes benchmark working directories through the same force-delete path every time.
/// </summary>
/// <remarks>
/// Benchmarks intentionally mutate files into states that ordinary recursive deletion is not expected to handle
/// reliably: read-only attributes, alternate streams, cloud attributes, links, and files that may have been locked
/// earlier in the run. Do not add a <see cref="Directory.Delete(string, bool)"/> fast path here. A managed delete
/// attempt can fail before the hardened cleanup path gets a chance to normalize entries, which can abort the whole
/// BenchmarkDotNet session during setup or cleanup. Keeping this helper on the Diagnostics force-delete path also
/// exercises the cleanup mechanism used by benchmarks that leave problematic file-system state behind.
/// </remarks>
internal static class BenchmarkDirectoryCleanup
{
    public static void ForceDelete(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        using var container = CreateContainer();
        container.Resolve<ForceDeleteDirectoryHelper>().DeleteDirectoryTree(directoryPath);
    }

    private static IContainer CreateContainer()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = new ContainerBuilder();
        builder.Populate(services);
        builder.AddInfrastructureServices();
        builder.AddDiagnosticsServices();

        return builder.Build();
    }
}
