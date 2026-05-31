namespace WinUiFileManager.Presentation.MessageLogging;

/// <summary>
/// One captured messenger message in the diagnostic log: when it was seen, its type name, an optional
/// routing id, and its formatted arguments. <see cref="MessageLogStore"/> holds these and projects
/// <see cref="Text"/> into the UI list.
/// </summary>
public sealed record MessageLogEntry(
    DateTime Timestamp,
    string MessageName,
    string Id,
    string Arguments)
{
    /// <summary>The single-line rendering shown in the log (time + name + optional id + arguments).</summary>
    public string Text =>
        string.IsNullOrEmpty(Id)
            ? $"{Timestamp:HH:mm:ss.fff} {MessageName} {Arguments}"
            : $"{Timestamp:HH:mm:ss.fff} {MessageName} Id={Id} {Arguments}";
}
