namespace WinUiFileManager.Benchmarks;

public static class BenchmarkConfig
{
    /// <summary>
    /// Creates the shared BenchmarkDotNet configuration for all benchmark classes.
    /// </summary>
    /// <remarks>
    /// Keep <c>[NativeMemoryProfiler]</c> on benchmarks that use it. The profiler intentionally performs an
    /// additional diagnostic process per benchmark case; do not remove the profiler just to reduce launch count.
    /// If run-count behavior needs to change, change the profiler configuration explicitly so native leak data is
    /// still collected.
    /// </remarks>
    public static IConfig Create() =>
        ManualConfig
            .Create(DefaultConfig.Instance)
            .WithBuildTimeout(TimeSpan.FromMinutes(5))
            .AddColumn(BenchmarkDotNet.Columns.CategoriesColumn.Default)
            .AddJob(Job.Default
                .WithStrategy(RunStrategy.Monitoring)
                .WithLaunchCount(1)
                .WithWarmupCount(1)
                .WithIterationCount(5)
                .WithInvocationCount(1)
                .WithPlatform(Platform.X64)
                .WithArguments([new MsBuildArgument("/p:Platform=x64")]))
            .StopOnFirstError();
}
