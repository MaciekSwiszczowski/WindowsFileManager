namespace WinUiFileManager.Benchmarks;

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
