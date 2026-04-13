using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiFileManager.Presentation.Dialogs;

public sealed class DeleteConfirmationDialog
{
    public static async Task<bool> ShowAsync(XamlRoot root, int itemCount, bool includesDirectories)
    {
        var itemDescription = includesDirectories
            ? $"{itemCount} item(s) including directories"
            : $"{itemCount} item(s)";

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Confirm Delete",
            Content = $"Permanently delete {itemDescription}?\nThis action cannot be undone.",
            PrimaryButtonText = "Delete Permanently",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        return result is ContentDialogResult.Primary;
    }
}
