using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Dialogs;

public sealed class PropertiesDialog
{
    public static async Task ShowAsync(XamlRoot root, IReadOnlyList<FileEntryViewModel> entries)
    {
        var content = entries.Count == 1
            ? BuildSingleItemContent(entries[0])
            : BuildMultiItemContent(entries);

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Properties",
            Content = content,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close
        };

        await dialog.ShowAsync();
    }

    private static StackPanel BuildSingleItemContent(FileEntryViewModel entry)
    {
        var panel = new StackPanel { Spacing = 4 };

        AddRow(panel, "Name", entry.Name);
        AddRow(panel, "Path", entry.FullPath);
        AddRow(panel, "Type", entry.IsDirectory ? "Directory" : "File");
        AddRow(panel, "Size", entry.Size);
        AddRow(panel, "Modified", entry.LastWriteTime);
        AddRow(panel, "Created", entry.Model.CreationTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        AddRow(panel, "Attributes", entry.Attributes);
        AddRow(panel, "File ID", entry.FileId);

        return panel;
    }

    private static StackPanel BuildMultiItemContent(IReadOnlyList<FileEntryViewModel> entries)
    {
        var panel = new StackPanel { Spacing = 4 };

        var fileCount = 0;
        var dirCount = 0;
        long totalSize = 0;

        foreach (var entry in entries)
        {
            if (entry.IsDirectory)
                dirCount++;
            else
                fileCount++;

            totalSize += entry.Model.Size;
        }

        AddRow(panel, "Selected", $"{entries.Count} items");
        AddRow(panel, "Files", fileCount.ToString());
        AddRow(panel, "Directories", dirCount.ToString());
        AddRow(panel, "Total size", FormatBytes(totalSize));

        return panel;
    }

    private static void AddRow(StackPanel panel, string label, string value)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12
        };
        Grid.SetColumn(labelBlock, 0);

        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        };
        Grid.SetColumn(valueBlock, 1);

        row.Children.Add(labelBlock);
        row.Children.Add(valueBlock);
        panel.Children.Add(row);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} B"
            : $"{size:F1} {units[unitIndex]}";
    }
}
