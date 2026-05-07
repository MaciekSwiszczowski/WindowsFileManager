namespace WinUiFileManager.Presentation.Dialogs;

public sealed record DialogResult(
    DialogButtonRole? ButtonRole,
    bool WasClosedByViewModel,
    bool WasUnavailable)
{
    public static DialogResult FromButton(DialogButtonRole role) =>
        new(role, WasClosedByViewModel: false, WasUnavailable: false);

    public static DialogResult Dismissed { get; } =
        new(ButtonRole: null, WasClosedByViewModel: false, WasUnavailable: false);

    public static DialogResult ClosedByViewModel { get; } =
        new(ButtonRole: null, WasClosedByViewModel: true, WasUnavailable: false);

    public static DialogResult Unavailable { get; } =
        new(ButtonRole: null, WasClosedByViewModel: false, WasUnavailable: true);
}
