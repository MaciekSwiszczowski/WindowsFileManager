namespace WinUiFileManager.Diagnostics.Inspector.Handlers;

/// <summary>
/// Pure formatter that composes the inspector's human-readable cloud "Status" label from a file's attributes and its
/// CldApi placeholder state, and owns the cloud-file flag constants those reads share. Separated from
/// <see cref="InspectorCloudDiagnosticsHandler"/> — which loads the facts — so the flag-combination logic, whose
/// CF_PLACEHOLDER_STATE semantics are subtle, can be unit-tested in isolation.
/// </summary>
internal static class CloudStatusFormatter
{
    // Cloud-files attribute flags not exposed by System.IO.FileAttributes (FILE_ATTRIBUTE_PINNED, etc.).
    public const FileAttributes Pinned = (FileAttributes)0x00080000;
    public const FileAttributes Unpinned = (FileAttributes)0x00100000;
    public const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
    public const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    /// <summary>Attribute bits that, if any is set, are sufficient evidence that a file is cloud-controlled.</summary>
    public const FileAttributes CloudEvidence = Pinned | Unpinned | RecallOnOpen | RecallOnDataAccess;

    // CF_PLACEHOLDER_STATE bit flags returned by CfGetPlaceholderStateFromAttributeTag.
    public const uint PlaceholderStateNone = 0x00000000;
    public const uint PlaceholderStateInSync = 0x00000008;
    // PARTIALLY_ON_DISK (0x20) = content not fully present locally → dehydrated/missing content. Deliberately NOT
    // PARTIAL (0x10): per cfapi docs PARTIAL only means "not ready to consume" and the content may still be fully on
    // disk (e.g. downloaded but pending VERIFICATION_REQUIRED), so PARTIAL must not drive the dehydrated label.
    public const uint PlaceholderStatePartiallyOnDisk = 0x00000020;

    /// <summary>
    /// Composes a comma-separated status from file attributes and the CldApi placeholder state
    /// (e.g. "Pinned, Hydrated, Synced"); empty when the inputs imply no cloud state.
    /// </summary>
    /// <remarks>
    /// Hydration is inferred from offline/recall attributes and the partially-on-disk placeholder bit; "Synced" comes
    /// from the in-sync placeholder bit. Each branch contributes a distinct label, so no de-duplication is needed.
    /// </remarks>
    public static string Format(FileAttributes attributes, uint placeholderState)
    {
        var labels = new List<string>();

        if ((attributes & Pinned) != 0)
        {
            labels.Add("Pinned");
        }

        if ((attributes & Unpinned) != 0)
        {
            labels.Add("Unpinned");
        }

        var isDehydrated =
            (attributes & FileAttributes.Offline) != 0
            || (attributes & RecallOnOpen) != 0
            || (attributes & RecallOnDataAccess) != 0
            || (placeholderState & PlaceholderStatePartiallyOnDisk) != 0;

        if (isDehydrated)
        {
            labels.Add("Dehydrated");
        }
        else if (placeholderState != PlaceholderStateNone || (attributes & FileAttributes.ReparsePoint) != 0)
        {
            labels.Add("Hydrated");
        }

        if ((placeholderState & PlaceholderStateInSync) != 0)
        {
            labels.Add("Synced");
        }

        return string.Join(", ", labels);
    }
}
