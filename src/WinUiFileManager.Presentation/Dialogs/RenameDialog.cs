using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiFileManager.Presentation.Dialogs;

public sealed class RenameDialog
{
    public static async Task<string?> ShowAsync(XamlRoot root, string currentName)
    {
        var nameBox = new TextBox
        {
            Text = currentName,
            AcceptsReturn = false,
            SelectionStart = 0,
            SelectionLength = currentName.Length
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Rename",
            Content = nameBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result is not ContentDialogResult.Primary)
        {
            return null;
        }

        var newName = nameBox.Text.Trim();
        return newName.Length > 0 && newName != currentName ? newName : null;
    }
}
