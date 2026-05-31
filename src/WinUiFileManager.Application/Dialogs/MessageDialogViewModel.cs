namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// View model for a simple, read-only message dialog (rendered via the
/// <see cref="DialogTemplateKeys.Message"/> template). Carries only the text to display; button
/// handling defaults to closing, so it does not implement <see cref="IDialogViewModel"/>.
/// </summary>
public sealed class MessageDialogViewModel
{
    /// <param name="message">The message text to display.</param>
    public MessageDialogViewModel(string message)
    {
        Message = message;
    }

    /// <summary>The message text shown to the user.</summary>
    public string Message { get; }
}
