using BenchmarkDotNet.Running;

namespace WinUiFileManager.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkProjectConfig.Load();

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig.Create());
    }
}
