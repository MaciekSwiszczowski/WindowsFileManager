namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Semantic role of a dialog button, mapping a <see cref="DialogButtonConfiguration"/> to a
/// <c>ContentDialog</c> slot and identifying which button the user pressed in <see cref="DialogResult"/>.
/// </summary>
public enum DialogButtonRole
{
    /// <summary>The affirmative/confirm action (e.g. OK, Rename).</summary>
    Primary,

    /// <summary>An alternative action distinct from confirm and cancel.</summary>
    Secondary,

    /// <summary>The dismiss/cancel action.</summary>
    Close,
}
