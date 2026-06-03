namespace WinUiFileManager.Benchmarks;

public static class BenchmarkProjectConfig
{
    public const string DefaultBenchmarkDirectory = "WinUiFileManagerBenchmarks";

    public static string BenchmarkDirectory { get; private set; } = DefaultBenchmarkDirectory;

    static BenchmarkProjectConfig()
    {
        Load();
    }

    public static void Load()
    {
        LoadFrom(Path.Combine(AppContext.BaseDirectory, "AppSettings.json"));
        LoadFrom(Path.Combine(Directory.GetCurrentDirectory(), "AppSettings.json"));
    }

    private static void LoadFrom(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (document.RootElement.TryGetProperty("BenchmarkDirectory", out var value)
            && value.GetString() is { Length: > 0 } benchmarkDirectory)
        {
            BenchmarkDirectory = benchmarkDirectory;
        }
    }
}
