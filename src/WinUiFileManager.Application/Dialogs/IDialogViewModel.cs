namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Implemented by dialog view models that need to react to button presses (e.g. validate input before
/// allowing the dialog to close). The dialog host calls <see cref="OnDialogButtonAsync"/> for each press.
/// </summary>
public interface IDialogViewModel
{
    /// <summary>
    /// Invoked when the user activates a button. May run validation or side effects.
    /// </summary>
    /// <param name="button">The role of the pressed button.</param>
    /// <param name="cancellationToken">Cancels the handling (e.g. dialog torn down).</param>
    /// <returns>Whether the dialog should close (<see cref="DialogButtonExecutionResult.Close"/>) or stay open.</returns>
    public Task<DialogButtonExecutionResult> OnDialogButtonAsync(
        DialogButtonRole button,
        CancellationToken cancellationToken);
}
