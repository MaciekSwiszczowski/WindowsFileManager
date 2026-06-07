namespace WinUiFileManager.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkProjectConfig.Load();

        var benchmarkAssembly = typeof(Program).Assembly;
        var runArgs = BenchmarkCategoryMenu.AddCategoryFilterIfSelected(args, benchmarkAssembly);

        BenchmarkSwitcher.FromAssembly(benchmarkAssembly).Run(runArgs, BenchmarkConfig.Create());
    }
}
