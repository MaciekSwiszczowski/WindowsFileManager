namespace WinUiFileManager.Presentation.ViewModels;

/// <summary>
/// The inspector's current selection state, which determines what the inspector panel renders
/// (empty state, the single-item field view, or the multi-selection summary).
/// </summary>
public enum FileInspectorSelectionMode
{
    /// <summary>Nothing is selected; the inspector shows its empty state.</summary>
    NoSelection,

    /// <summary>Exactly one item is selected; per-field diagnostics are shown.</summary>
    SingleSelection,

    /// <summary>More than one item is selected; only an aggregate summary is shown.</summary>
    MultiSelection,
}
