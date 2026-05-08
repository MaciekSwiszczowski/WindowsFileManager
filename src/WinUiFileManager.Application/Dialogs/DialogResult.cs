namespace WinUiFileManager.Application.Dialogs;

public sealed record DialogResult(DialogButtonRole? ButtonRole, bool WasUnavailable)
{
    public static DialogResult FromButton(DialogButtonRole role) =>
        new(role, WasUnavailable: false);

    public static DialogResult Dismissed { get; } =
        new(ButtonRole: null, WasUnavailable: false);

    public static DialogResult Unavailable { get; } =
        new(ButtonRole: null, WasUnavailable: true);
}
