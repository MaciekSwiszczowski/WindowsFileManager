namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class RenameDialogViewModel : ObservableObject, IDialogViewModel
{
    private readonly FileSystemEntryModel _entry;

    public RenameDialogViewModel(FileSystemEntryModel entry)
    {
        _entry = entry;
        NewName = entry.Name;
    }

    public string OriginalName => _entry.Name;

    public string ErrorMessage
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ErrorVisibility));
            }
        }
    } = string.Empty;

    public Visibility ErrorVisibility =>
        string.IsNullOrWhiteSpace(ErrorMessage)
            ? Visibility.Collapsed
            : Visibility.Visible;

    [ObservableProperty]
    public partial string NewName { get; set; }

    public Task<DialogButtonExecutionResult> OnDialogButtonAsync(
        DialogButtonRole button,
        CancellationToken cancellationToken)
    {
        if (button is not DialogButtonRole.Primary)
        {
            return Task.FromResult(DialogButtonExecutionResult.Close);
        }

        var newName = NewName.Trim();
        if (newName.Length == 0)
        {
            ErrorMessage = "Enter a file name.";
            return Task.FromResult(DialogButtonExecutionResult.KeepOpen);
        }

        ErrorMessage = string.Empty;
        return Task.FromResult(DialogButtonExecutionResult.Close);
    }
}
