using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Writes diagnostics values into the inspector field view models. Builds a key-&gt;field lookup once from the
/// category tree, then exposes typed <c>Show*Diagnostics</c> methods that the inspector and deferred loaders call
/// to populate fields. The single owner of how each diagnostics record maps onto field <c>Key</c>s.
/// </summary>
/// <remarks>
/// All writes here mutate observable field view models, so callers must invoke these on the UI thread. Diagnostics
/// with "no evidence" are normalized to friendly placeholder text (e.g. "No alternate streams") rather than blanks.
/// </remarks>
internal sealed class InspectorFieldValueUpdater
{
    private readonly FileEntryDisplayStringCache _displayStringCache;
    private readonly IReadOnlyDictionary<string, InspectorFieldViewModelBase> _fields;

    /// <summary>Flattens all category fields into a case-insensitive key lookup used by <c>SetValue</c>.</summary>
    public InspectorFieldValueUpdater(
        IReadOnlyList<InspectorCategoryViewModel> categories,
        FileEntryDisplayStringCache displayStringCache)
    {
        _displayStringCache = displayStringCache;
        _fields = categories
            .SelectMany(static category => category.Fields)
            .ToDictionary(static field => field.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resets all fields and fills the synchronously-available basics (name, path, type, size, attributes, and the
    /// fast NTFS timestamps) for the newly selected item. Parent-entry rows (null model) just clear the fields.
    /// </summary>
    public void ShowImmediateSelection(FileListingRow selectedItem)
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

    /// <summary>Populates the Streams category from alternate-data-stream diagnostics.</summary>
    public void ShowStreamsDiagnostics(FileStreamDiagnosticsDetails diagnostics)
    {
        SetValue("Alternate Stream Count", diagnostics.AlternateStreamCount.ToString());
        SetValue(
            "Alternate Streams",
            diagnostics.AlternateStreams.Count > 0
                ? string.Join(Environment.NewLine, diagnostics.AlternateStreams)
                : "No alternate streams");
    }

    /// <summary>Populates the Ids and (UTC-as-local) NTFS timestamp fields from identity diagnostics, overwriting the fast timestamps with authoritative NTFS values.</summary>
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

    /// <summary>Populates the Locks category; when there is no positive lock evidence, marks "Is locked" False and blanks the detail fields.</summary>
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

    /// <summary>Populates the Links category; shows a "No link or reparse data" placeholder when no link evidence is present.</summary>
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

    /// <summary>Populates the Security category (owner/group, DACL/SACL summaries, inherited/protected flags).</summary>
    public void ShowSecurityDiagnostics(FileSecurityDiagnosticsDetails diagnostics)
    {
        SetValue("Owner", diagnostics.Owner);
        SetValue("Group", diagnostics.Group);
        SetValue("DACL Summary", diagnostics.DaclSummary);
        SetValue("SACL Summary", diagnostics.SaclSummary);
        SetValue("Inherited", InspectorFieldFormatting.OptionalBoolean(diagnostics.Inherited));
        SetValue("Protected", InspectorFieldFormatting.OptionalBoolean(diagnostics.Protected));
    }

    /// <summary>Populates the Cloud category; shows "Not cloud controlled" and blanks the rest when the item is not cloud-managed.</summary>
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

    /// <summary>Populates the Thumbnails category with the decoded image (built off-UI by the loader) plus availability/association text.</summary>
    public void ShowThumbnailDiagnostics(FileThumbnailDiagnosticsDetails diagnostics, ImageSource? thumbnailSource)
    {
        SetThumbnailSource(thumbnailSource);
        SetValue("Thumbnail", thumbnailSource is null ? string.Empty : "Preview");
        SetValue("Has Thumbnail", InspectorFieldFormatting.HasThumbnail(diagnostics) ? "Yes" : "No");
        SetValue("Association", diagnostics.ProgId);
    }

    /// <summary>Sets the loading flag on the fields addressed by <paramref name="keys"/>; unknown keys are ignored.</summary>
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

    /// <summary>Clears every field back to its empty/visible/not-loading default (and nulls any thumbnail) before a new selection is shown.</summary>
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

    /// <summary>Sets the six attribute fields (incl. the writable toggles) from the model's <see cref="FileAttributes"/>.</summary>
    private void SetAttributeFlags(FileAttributes attributes)
    {
        SetValue("Read Only", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.ReadOnly)));
        SetValue("Hidden", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Hidden)));
        SetValue("Archive", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Archive)));
        SetValue("Encrypted", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Encrypted)));
        SetValue("Compressed", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.Compressed)));
        SetValue("Reparse Point", InspectorFieldFormatting.Flag(attributes.HasFlag(FileAttributes.ReparsePoint)));
    }

    /// <summary>Sets a field's value by key; no-op when the key is not present in the field set.</summary>
    private void SetValue(string key, string value)
    {
        if (_fields.TryGetValue(key, out var field))
        {
            field.Value = value;
        }
    }

    /// <summary>Assigns the thumbnail image to the "Thumbnail" field when it is a thumbnail field.</summary>
    private void SetThumbnailSource(ImageSource? thumbnailSource)
    {
        if (_fields.TryGetValue("Thumbnail", out var field)
            && field is InspectorThumbnailFieldViewModel thumbnailField)
        {
            thumbnailField.ThumbnailSource = thumbnailSource;
        }
    }
}
