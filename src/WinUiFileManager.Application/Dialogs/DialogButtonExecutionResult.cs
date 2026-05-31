namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Returned by <see cref="IDialogViewModel.OnDialogButtonAsync"/> to tell the host whether a button
/// press should dismiss the dialog. Lets a view model veto closing (e.g. when validation fails).
/// </summary>
/// <param name="ShouldClose"><see langword="true"/> to close the dialog; <see langword="false"/> to keep it open.</param>
public readonly record struct DialogButtonExecutionResult(bool ShouldClose)
{
    /// <summary>Result that closes the dialog.</summary>
    public static DialogButtonExecutionResult Close { get; } = new(ShouldClose: true);

    /// <summary>Result that keeps the dialog open (e.g. to show a validation error).</summary>
    public static DialogButtonExecutionResult KeepOpen { get; } = new(ShouldClose: false);
}
