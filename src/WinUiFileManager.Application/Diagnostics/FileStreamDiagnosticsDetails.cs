namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Immutable description of a file's NTFS alternate data streams, shown in the inspector's Streams
/// section. Produced by the Diagnostics layer in reply to
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// </summary>
/// <param name="AlternateStreamCount">Count of alternate streams.</param>
/// <param name="AlternateStreams">Names (and sizes) of the alternate streams, formatted for display.</param>
public sealed record FileStreamDiagnosticsDetails(int AlternateStreamCount, IReadOnlyList<string> AlternateStreams)
{
    /// <summary>Sentinel for a file with no alternate streams (count 0, empty list).</summary>
    public static FileStreamDiagnosticsDetails Empty { get; } = new(0, []);
}
