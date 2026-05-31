using CommunityToolkit.WinUI.Converters;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal static class InspectorFieldFormatting
{
    private static readonly FileSizeToFriendlyStringConverter FileSizeConverter = new();

    public static string LocalTime(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : value.ToString("yyyy-MM-dd HH:mm:ss");

    public static string UtcAsLocal(DateTime value) =>
        value == DateTime.MinValue
            ? string.Empty
            : LocalTime(value.ToLocalTime());

    public static string Flag(bool value) => value ? "Yes" : "No";

    public static string OptionalBoolean(bool? value) =>
        value switch
        {
            true => "Yes",
            false => "No",
            _ => string.Empty,
        };

    public static bool HasPositiveLockEvidence(FileLockDiagnostics diagnostics) =>
        diagnostics.InUse == true
        || diagnostics.LockBy.Count > 0
        || diagnostics.LockPids.Count > 0
        || diagnostics.LockServices.Count > 0;

    public static bool HasLinkEvidence(FileLinkDiagnosticsDetails diagnostics) =>
        !string.IsNullOrWhiteSpace(diagnostics.LinkTarget)
        || !string.IsNullOrWhiteSpace(diagnostics.LinkStatus)
        || !string.IsNullOrWhiteSpace(diagnostics.ReparseTag)
        || !string.IsNullOrWhiteSpace(diagnostics.ReparseData)
        || !string.IsNullOrWhiteSpace(diagnostics.ObjectId);

    public static bool HasThumbnail(FileThumbnailDiagnosticsDetails diagnostics) =>
        diagnostics.ThumbnailBytes is { Length: > 0 };

    public static string FileId(NtfsFileId fileId) =>
        fileId == NtfsFileId.None ? "Unavailable" : fileId.HexDisplay;

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
