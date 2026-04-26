namespace WinUiFileManager.Presentation.MessageLogging;

public sealed record MessageLogEntry(
    DateTime Timestamp,
    string MessageName,
    string Id,
    string Arguments)
{
    public string Text =>
        string.IsNullOrEmpty(Id)
            ? $"{Timestamp:HH:mm:ss.fff} {MessageName} {Arguments}"
            : $"{Timestamp:HH:mm:ss.fff} {MessageName} Id={Id} {Arguments}";
}
