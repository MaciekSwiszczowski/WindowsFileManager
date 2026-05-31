using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinUiFileManager.Application.FileEntries;

namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// View model for the rename dialog: holds the proposed new name, exposes a validation error, and
/// vetoes closing on the primary button when the name is empty. Lives in Application (UI-framework-free)
/// and is rendered via the <see cref="DialogTemplateKeys.Rename"/> template.
/// </summary>
/// <remarks>
/// Uses hand-rolled <see cref="INotifyPropertyChanged"/> rather than the toolkit generators because the
/// Application layer is framework-light and does not reference the MVVM source generators here.
/// </remarks>
public sealed class RenameDialogViewModel : IDialogViewModel, INotifyPropertyChanged
{
    private readonly FileSystemEntryModel _entry;
    private string _newName;

    /// <param name="entry">The entry being renamed; its name seeds <see cref="NewName"/> and is exposed as <see cref="OriginalName"/>.</param>
    public RenameDialogViewModel(FileSystemEntryModel entry)
    {
        _entry = entry;
        _newName = entry.Name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The entry's original name, shown for reference.</summary>
    public string OriginalName => _entry.Name;

    /// <summary>Validation message shown to the user; empty when input is valid. Raises change notifications for <see cref="HasError"/> too.</summary>
    public string ErrorMessage
    {
        get;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    } = string.Empty;

    /// <summary>Whether <see cref="ErrorMessage"/> is currently set (drives error UI visibility).</summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>The proposed new name, two-way bound to the dialog's text box.</summary>
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

    /// <summary>
    /// Handles a button press: non-primary buttons close immediately; the primary (Rename) button keeps
    /// the dialog open with an error when the trimmed name is empty, otherwise clears the error and closes.
    /// Synchronous work wrapped in completed tasks because no I/O is performed here.
    /// </summary>
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
