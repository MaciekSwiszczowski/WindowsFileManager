namespace WinUiFileManager.Application.Diagnostics.Profiling;

/// <summary>
/// Identifies a deferred inspector diagnostics handler for profiling control. One value per handler that answers
/// <see cref="WinUiFileManager.Application.Messages.RequestMessages.Inspector.InspectorDiagnosticsRequestMessage"/>.
/// The presentation layer maps its visual <c>FileInspectorCategory</c> sections onto these (Identity feeds both
/// the NTFS and Ids sections).
/// </summary>
public enum DiagnosticsCategory
{
    /// <summary>NTFS identity + authoritative timestamps handler.</summary>
    Identity,

    /// <summary>Lock / in-use diagnostics handler.</summary>
    Locks,

    /// <summary>Link / reparse diagnostics handler.</summary>
    Links,

    /// <summary>Alternate data stream diagnostics handler.</summary>
    Streams,

    /// <summary>Security descriptor diagnostics handler.</summary>
    Security,

    /// <summary>Shell thumbnail diagnostics handler.</summary>
    Thumbnail,

    /// <summary>Cloud / placeholder diagnostics handler.</summary>
    Cloud,
}
