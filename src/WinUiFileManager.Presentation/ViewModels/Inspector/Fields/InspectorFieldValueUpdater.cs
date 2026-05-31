using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

internal sealed class InspectorFieldValueUpdater
{
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private readonly IReadOnlyDictionary<string, InspectorFieldViewModelBase> _fields;

    public InspectorFieldValueUpdater(
        IReadOnlyList<InspectorCategoryViewModel> categories,
        FileEntryDisplayStringCache displayStringCache)
    {
        _displayStringCache = displayStringCache;
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);
    }

    public void ShowImmediateSelection(SpecFileEntryViewModel selectedItem)
    {
        ClearValues();

        if (selectedItem.Model is not { } model)
        {
            return;
        }

        SetValue("Name", model.Name);
        SetValue("Full Path", model.FullPath.DisplayPath);
        SetValue("Type", model.Kind == ItemKind.Directory ? "Folder" : "File");
        SetValue("Extension", _displayStringCache.GetExtension(model.Extension));
        SetValue("Size", InspectorFieldFormatting.Size(model));
        SetValue("Attributes", _displayStringCache.GetInspectorAttributes(model.Attributes));

        SetValue("Created", InspectorFieldFormatting.LocalTime(model.CreationTime));
        SetValue("Modified", InspectorFieldFormatting.LocalTime(model.LastWriteTime));
        SetAttributeFlags(model.Attributes);
    }

    public void ShowStreamsDiagnostics(FileStreamDiagnosticsDetails diagnostics)
    {
        var streamCount = string.IsNullOrWhiteSpace(diagnostics.AlternateStreamCount)
            ? "0"
            : diagnostics.AlternateStreamCount;

        SetValue("Alternate Stream Count", streamCount);
        SetValue(
            "Alternate Streams",
            diagnostics.AlternateStreams.Count > 0
                ? string.Join(Environment.NewLine, diagnostics.AlternateStreams)
                : "No alternate streams");
    }

    public void ShowIdentityDiagnostics(InspectorIdentityDiagnosticsDetails diagnostics)
    {
        SetValue("Created", InspectorFieldFormatting.UtcAsLocal(diagnostics.NtfsMetadata.CreationTimeUtc));
        SetValue("Accessed", InspectorFieldFormatting.UtcAsLocal(diagnostics.NtfsMetadata.LastAccessTimeUtc));
        SetValue("Modified", InspectorFieldFormatting.UtcAsLocal(diagnostics.NtfsMetadata.LastWriteTimeUtc));
        SetValue("MFT Changed", InspectorFieldFormatting.UtcAsLocal(diagnostics.NtfsMetadata.ChangeTimeUtc));

        SetValue("File ID", InspectorFieldFormatting.FileId(diagnostics.Identity.FileId));
        SetValue("Volume Serial", diagnostics.Identity.VolumeSerial);
        SetValue("File Index (64-bit)", diagnostics.Identity.LegacyFileIndex);
        SetValue("Hard Link Count", diagnostics.Identity.HardLinkCount);
        SetValue("Final Path", diagnostics.Identity.FinalPath);
    }

    public void ShowLockDiagnostics(FileLockDiagnostics diagnostics)
    {
        if (!InspectorFieldFormatting.HasPositiveLockEvidence(diagnostics))
        {
            SetValue("Is locked", "False");
            SetValue("In Use", string.Empty);
            SetValue("Locked By", string.Empty);
            SetValue("Lock PIDs", string.Empty);
            SetValue("Lock Services", string.Empty);
            return;
        }

        SetValue("Is locked", "True");
        SetValue("In Use", InspectorFieldFormatting.OptionalBoolean(diagnostics.InUse));
        SetValue("Locked By", diagnostics.LockBy.Count == 0 ? string.Empty : string.Join(Environment.NewLine, diagnostics.LockBy));
        SetValue("Lock PIDs", diagnostics.LockPids.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockPids));
        SetValue("Lock Services", diagnostics.LockServices.Count == 0 ? string.Empty : string.Join(", ", diagnostics.LockServices));
    }

    public void ShowLinkDiagnostics(FileLinkDiagnosticsDetails diagnostics)
    {
        if (!InspectorFieldFormatting.HasLinkEvidence(diagnostics))
        {
            SetValue("Link Target", "No link or reparse data");
            SetValue("Link Status", string.Empty);
            SetValue("Reparse Tag", string.Empty);
            SetValue("Reparse Data", string.Empty);
            SetValue("Object ID", string.Empty);
            return;
        }

        SetValue("Link Target", diagnostics.LinkTarget);
        SetValue("Link Status", diagnostics.LinkStatus);
        SetValue("Reparse Tag", diagnostics.ReparseTag);
        SetValue("Reparse Data", diagnostics.ReparseData);
        SetValue("Object ID", diagnostics.ObjectId);
    }

    public void ShowSecurityDiagnostics(FileSecurityDiagnosticsDetails diagnostics)
    {
        SetValue("Owner", diagnostics.Owner);
        SetValue("Group", diagnostics.Group);
        SetValue("DACL Summary", diagnostics.DaclSummary);
        SetValue("SACL Summary", diagnostics.SaclSummary);
        SetValue("Inherited", InspectorFieldFormatting.OptionalBoolean(diagnostics.Inherited));
        SetValue("Protected", InspectorFieldFormatting.OptionalBoolean(diagnostics.Protected));
    }

    public void ShowCloudDiagnostics(FileCloudDiagnosticsDetails diagnostics)
    {
        if (!diagnostics.IsCloudControlled)
        {
            SetValue("Status", "Not cloud controlled");
            SetValue("Provider", string.Empty);
            SetValue("Sync Root", string.Empty);
            SetValue("Root ID", string.Empty);
            SetValue("Provider ID", string.Empty);
            SetValue("Available", string.Empty);
            SetValue("Transfer", string.Empty);
            SetValue("Custom", string.Empty);
            return;
        }

        SetValue("Status", diagnostics.Status);
        SetValue("Provider", diagnostics.Provider);
        SetValue("Sync Root", diagnostics.SyncRoot);
        SetValue("Root ID", diagnostics.SyncRootId);
        SetValue("Provider ID", diagnostics.ProviderId);
        SetValue("Available", diagnostics.Available);
        SetValue("Transfer", diagnostics.Transfer);
        SetValue("Custom", diagnostics.Custom);
    }

    public void ShowThumbnailDiagnostics(FileThumbnailDiagnosticsDetails diagnostics, ImageSource? thumbnailSource)
    {
        SetThumbnailSource(thumbnailSource);
        SetValue("Thumbnail", thumbnailSource is null ? string.Empty : "Preview");
        SetValue("Has Thumbnail", InspectorFieldFormatting.HasThumbnail(diagnostics) ? "Yes" : "No");
        SetValue("Association", diagnostics.ProgId);
    }

    public void SetLoading(IEnumerable<string> keys, bool isLoading)
    {
        foreach (var key in keys)
        {
            if (_fields.TryGetValue(key, out var field))
            {
                field.IsLoading = isLoading;
            }
        }
    }

    private void ClearValues()
    {
        foreach (var field in _fields.Values)
        {
            field.Value = string.Empty;
            field.IsLoading = false;
            field.IsVisible = true;

            if (field is InspectorThumbnailFieldViewModel thumbnailField)
            {
                thumbnailField.ThumbnailSource = null;
            }
        }
    }

    private void SetAttributeFlags(FileAttributes attributes)
    {
        SetValue("Read Only", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.ReadOnly)));
        SetValue("Hidden", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Hidden)));
        SetValue("Archive", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Archive)));
        SetValue("Encrypted", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Encrypted)));
        SetValue("Compressed", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Compressed)));
        SetValue("Reparse Point", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.ReparsePoint)));
    }

    private void SetValue(string key, string value)
    {
        if (_fields.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    private void SetThumbnailSource(ImageSource? thumbnailSource)
    {
        if (_fields.TryGetValue("Thumbnail", out var field)
            && field is InspectorThumbnailFieldViewModel thumbnailField)
        {
            thumbnailField.ThumbnailSource = thumbnailSource;
        }
    }
}
