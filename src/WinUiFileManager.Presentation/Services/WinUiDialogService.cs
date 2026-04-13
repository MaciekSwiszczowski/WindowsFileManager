using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Results;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.Services;

public sealed class WinUiDialogService : IDialogService
{
    public XamlRoot? XamlRoot { get; set; }

    public async Task<bool> ShowDeleteConfirmationAsync(
        int itemCount, bool includesDirectories, CancellationToken ct)
    {
        if (XamlRoot is null)
            return false;

        var message = includesDirectories
            ? $"Permanently delete {itemCount} item(s) including directories?"
            : $"Permanently delete {itemCount} item(s)?";

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirm Delete",
            Content = message,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowCreateFolderDialogAsync(CancellationToken ct)
    {
        if (XamlRoot is null)
            return null;

        var textBox = new TextBox { PlaceholderText = "Folder name" };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Create Folder",
            Content = textBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        var name = textBox.Text.Trim();
        return name.Length > 0 ? name : null;
    }

    public async Task<string?> ShowRenameDialogAsync(string currentName, CancellationToken ct)
    {
        if (XamlRoot is null)
            return null;

        var textBox = new TextBox { Text = currentName };
        textBox.SelectAll();

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Rename",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return null;

        var name = textBox.Text.Trim();
        return name.Length > 0 && name != currentName ? name : null;
    }

    public async Task<CollisionPolicy> ShowCollisionDialogAsync(
        NormalizedPath sourcePath, NormalizedPath destinationPath, CancellationToken ct)
    {
        if (XamlRoot is null)
            return CollisionPolicy.Cancel;

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = $"A file already exists at:\n{destinationPath.DisplayPath}",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Source:\n{sourcePath.DisplayPath}",
            TextWrapping = TextWrapping.Wrap,
        });

        var combo = new ComboBox
        {
            ItemsSource = new[]
            {
                "Overwrite this file",
                "Overwrite all",
                "Skip this file",
                "Skip all",
                "Rename target",
                "Rename all",
            },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        content.Children.Add(combo);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "File Conflict",
            Content = content,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return CollisionPolicy.Cancel;

        return combo.SelectedIndex switch
        {
            0 => CollisionPolicy.Overwrite,
            1 => CollisionPolicy.OverwriteAll,
            2 => CollisionPolicy.Skip,
            3 => CollisionPolicy.SkipAll,
            4 => CollisionPolicy.RenameTarget,
            5 => CollisionPolicy.RenameAll,
            _ => CollisionPolicy.Cancel,
        };
    }

    public async Task ShowPropertiesAsync(
        IReadOnlyList<FileSystemEntryModel> entries, CancellationToken ct)
    {
        if (XamlRoot is null || entries.Count == 0)
            return;

        var panel = new StackPanel { Spacing = 6 };

        if (entries.Count == 1)
        {
            var entry = entries[0];
            AddProperty(panel, "Name", entry.Name);
            AddProperty(panel, "Path", entry.FullPath.DisplayPath);
            AddProperty(panel, "Type", entry.Kind.ToString());
            if (entry.Kind != ItemKind.Directory)
                AddProperty(panel, "Size", FormatSize(entry.Size));
            AddProperty(panel, "Created", entry.CreationTimeUtc.ToLocalTime().ToString("G"));
            AddProperty(panel, "Modified", entry.LastWriteTimeUtc.ToLocalTime().ToString("G"));
            AddProperty(panel, "Attributes", entry.Attributes.ToString());
        }
        else
        {
            AddProperty(panel, "Items selected", entries.Count.ToString());
            var totalSize = entries.Where(e => e.Kind != ItemKind.Directory).Sum(e => e.Size);
            AddProperty(panel, "Total size (files)", FormatSize(totalSize));
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Properties",
            Content = panel,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    public async Task ShowOperationResultAsync(OperationSummary summary, CancellationToken ct)
    {
        if (XamlRoot is null)
            return;

        var sb = new StringBuilder();
        sb.AppendLine($"Operation: {summary.Type}");
        sb.AppendLine($"Status: {summary.Status}");
        sb.AppendLine($"Duration: {summary.Duration.TotalSeconds:F1}s");
        sb.AppendLine();
        sb.AppendLine($"Total: {summary.TotalItems}  |  Succeeded: {summary.SucceededCount}  |  Failed: {summary.FailedCount}  |  Skipped: {summary.SkippedCount}");

        if (summary.WasCancelled)
            sb.AppendLine("Operation was cancelled.");

        var failedItems = summary.ItemResults.Where(r => !r.Succeeded).ToList();
        if (failedItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Failed items:");
            foreach (var item in failedItems.Take(20))
            {
                sb.AppendLine($"  {item.SourcePath.DisplayPath}");
                if (item.Error is not null)
                    sb.AppendLine($"    {item.Error.Message}");
            }

            if (failedItems.Count > 20)
                sb.AppendLine($"  ... and {failedItems.Count - 20} more");
        }

        if (!string.IsNullOrEmpty(summary.Message))
        {
            sb.AppendLine();
            sb.AppendLine(summary.Message);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Operation Result",
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = sb.ToString(),
                    TextWrapping = TextWrapping.Wrap,
                },
                MaxHeight = 400,
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    private static void AddProperty(StackPanel panel, string label, string value)
    {
        panel.Children.Add(new TextBlock
        {
            Text = $"{label}: {value}",
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
    };
}
