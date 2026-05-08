using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Dialogs;

public sealed class RenameDialogViewModel : IDialogViewModel, INotifyPropertyChanged
{
    private readonly FileSystemEntryModel _entry;
    private string _errorMessage = string.Empty;
    private string _newName;

    public RenameDialogViewModel(FileSystemEntryModel entry)
    {
        _entry = entry;
        _newName = entry.Name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string OriginalName => _entry.Name;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
            {
                return;
            }

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string NewName
    {
        get => _newName;
        set
        {
            if (_newName == value)
            {
                return;
            }

            _newName = value;
            OnPropertyChanged();
        }
    }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
