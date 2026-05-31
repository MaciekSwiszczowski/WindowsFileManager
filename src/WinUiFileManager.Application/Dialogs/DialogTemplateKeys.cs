namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Well-known XAML resource keys naming the <c>DataTemplate</c> that renders each dialog view model.
/// Passed as <see cref="ShowDialogMessage.ContentTemplateKey"/> so the Application layer can request a
/// specific template without referencing Presentation types.
/// </summary>
public static class DialogTemplateKeys
{
    /// <summary>Template key for the rename dialog (<see cref="RenameDialogViewModel"/>).</summary>
    public const string Rename = "RenameDialogDataTemplate";

    /// <summary>Template key for the simple message dialog (<see cref="MessageDialogViewModel"/>).</summary>
    public const string Message = "MessageDialogDataTemplate";
}
