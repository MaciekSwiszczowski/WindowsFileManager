using CommunityToolkit.WinUI.Converters;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Pure formatting/predicate helpers shared by <see cref="InspectorFieldValueUpdater"/> for turning diagnostics
/// values into the strings shown in inspector fields. Stateless except for a cached size converter.
/// </summary>
internal static class InspectorFieldFormatting
{
    /// <summary>Shared size formatter; reused to avoid allocating a converter per call.</summary>
    private static readonly FileSizeToFriendlyStringConverter FileSizeConverter = new();

    /// <summary>Formats a local <see cref="DateTime"/> as <c>yyyy-MM-dd HH:mm:ss</c>; empty for <see cref="DateTime.MinValue"/> (unset).</summary>
    public static string LocalTime(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : value.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>Converts a UTC timestamp to local time and formats it; empty for <see cref="DateTime.MinValue"/> (unset).</summary>
    public static string UtcAsLocal(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : LocalTime(value.ToLocalTime());

    /// <summary>Maps a definite boolean to "Yes"/"No".</summary>
    public static string Flag(bool value) => value ? "Yes" : "No";

    /// <summary>Maps a nullable boolean to "Yes"/"No"/empty (empty when unknown).</summary>
    public static string OptionalBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => string.Empty,
        };

    /// <summary>True when any lock signal (in-use, owners, PIDs, services) indicates the item is actually locked.</summary>
    public static bool HasPositiveLockEvidence(FileLockDiagnostics diagnostics) =>
        diagnostics.InUse == true
        || diagnostics.LockBy.Count > 0
        || diagnostics.LockPids.Count > 0
        || diagnostics.LockServices.Count > 0;

    /// <summary>True when any link/reparse signal is present (target, status, tag, data, or object id).</summary>
    public static bool HasLinkEvidence(FileLinkDiagnosticsDetails diagnostics) =>
        !string.IsNullOrWhiteSpace(diagnostics.LinkTarget)
        || !string.IsNullOrWhiteSpace(diagnostics.LinkStatus)
        || !string.IsNullOrWhiteSpace(diagnostics.ReparseTag)
        || !string.IsNullOrWhiteSpace(diagnostics.ReparseData)
        || !string.IsNullOrWhiteSpace(diagnostics.ObjectId);

    /// <summary>True when the diagnostics carry actual thumbnail bytes.</summary>
    public static bool HasThumbnail(FileThumbnailDiagnosticsDetails diagnostics) =>
        diagnostics.ThumbnailBytes is { Length: > 0 };

    /// <summary>Formats an NTFS file id as its hex display, or "Unavailable" for <see cref="NtfsFileId.None"/>.</summary>
    public static string FileId(NtfsFileId fileId) =>
        fileId == NtfsFileId.None ? "Unavailable" : fileId.HexDisplay;

    /// <summary>Formats a model's size as a friendly string; empty when the size is unknown.</summary>
    public static string Size(FileSystemEntryModel model)
    {
        if (model.Size is not { } size)
        {
            return string.Empty;
        }

        return FileSizeConverter.Convert(size, typeof(string), string.Empty, string.Empty) as string
            ?? string.Empty;
    }
}
