using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiFileManager.Presentation.Dialogs;

public sealed class CreateFolderDialog
{
    public static async Task<string?> ShowAsync(XamlRoot root)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Folder name",
            AcceptsReturn = false
        };

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "New Folder",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        if (result is not ContentDialogResult.Primary)
        {
            return null;
        }

        var name = nameBox.Text.Trim();
        return name.Length > 0 ? name : null;
    }
}
