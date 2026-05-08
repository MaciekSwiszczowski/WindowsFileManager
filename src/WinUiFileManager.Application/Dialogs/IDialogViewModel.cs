namespace WinUiFileManager.Application.Dialogs;

public interface IDialogViewModel
{
    public Task<DialogButtonExecutionResult> OnDialogButtonAsync(
        DialogButtonRole button,
        CancellationToken cancellationToken);
}
