namespace WinUiFileManager.Application.Dialogs;

public readonly record struct DialogButtonExecutionResult(bool ShouldClose)
{
    public static DialogButtonExecutionResult Close { get; } = new(ShouldClose: true);

    public static DialogButtonExecutionResult KeepOpen { get; } = new(ShouldClose: false);
}
