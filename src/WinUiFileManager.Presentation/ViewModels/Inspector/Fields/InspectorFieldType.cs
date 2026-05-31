namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Discriminates the kind of inspector field so the view can pick the right cell template
/// (plain text, an image preview, or an interactive toggle).
/// </summary>
public enum InspectorFieldType
{
    /// <summary>A read-only text value field.</summary>
    Text,

    /// <summary>A thumbnail/image preview field.</summary>
    Thumbnail,

    /// <summary>An interactive on/off field backed by a writable attribute.</summary>
    Toggle,
}
