namespace WinUiFileManager.Domain.ValueObjects;

public sealed record ParallelExecutionOptions
{
    public ParallelExecutionOptions(bool enabled = false, int maxDegreeOfParallelism = 4)
    {
        Enabled = enabled;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
    }

    public bool Enabled { get; init; }

    public int MaxDegreeOfParallelism { get; init; }
}
