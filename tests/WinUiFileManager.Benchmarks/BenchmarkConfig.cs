namespace WinUiFileManager.Benchmarks;

public static class BenchmarkConfig
{
    public static IConfig Create() =>
        ManualConfig
            .Create(DefaultConfig.Instance)
            .AddJob(Job.Default
                .WithStrategy(RunStrategy.Monitoring)
                .WithPlatform(Platform.X64)
                .WithArguments([new MsBuildArgument("/p:Platform=x64")]))
            .StopOnFirstError();
}
