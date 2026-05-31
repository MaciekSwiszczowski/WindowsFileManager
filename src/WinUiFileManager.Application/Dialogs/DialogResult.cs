namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Outcome of a <see cref="ShowDialogMessage"/> request: which button (if any) closed the dialog, or
/// whether the dialog could not be shown at all. This is the reply value the message sender awaits.
/// </summary>
/// <param name="ButtonRole">The role of the button that closed the dialog, or <see langword="null"/> if dismissed without one.</param>
/// <param name="WasUnavailable"><see langword="true"/> when the dialog could not be displayed (e.g. no <c>XamlRoot</c>).</param>
public sealed record DialogResult(DialogButtonRole? ButtonRole, bool WasUnavailable)
{
    /// <summary>Result indicating the dialog closed via the button with the given <paramref name="role"/>.</summary>
    public static DialogResult FromButton(DialogButtonRole role) =>
        new(role, WasUnavailable: false);

    /// <summary>Result indicating the dialog was dismissed without a recognized button (e.g. Esc/back).</summary>
    public static DialogResult Dismissed { get; } =
        new(ButtonRole: null, WasUnavailable: false);

    /// <summary>Result indicating the dialog could not be shown at all.</summary>
    public static DialogResult Unavailable { get; } =
        new(ButtonRole: null, WasUnavailable: true);
}
