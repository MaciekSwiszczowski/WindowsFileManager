namespace WinUiFileManager.Presentation.Dialogs;

public interface IDialogViewModel
{
    Task<DialogButtonExecutionResult> OnDialogButtonAsync(
        DialogButtonRole button,
        CancellationToken cancellationToken);
}
