namespace WinUiFileManager.Domain.ValueObjects;

public sealed record ParallelExecutionOptions(
    bool Enabled = false,
    int MaxDegreeOfParallelism = 4);
