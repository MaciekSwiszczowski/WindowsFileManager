using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiFileManager.Application.Abstractions;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.Events;
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
        {
            return false;
        }

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
        {
            return null;
        }

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
        {
            return null;
        }

        var name = textBox.Text.Trim();
        return name.Length > 0 ? name : null;
    }

    public Task<IOperationProgressDialog> ShowOperationProgressAsync(
        OperationType operationType,
        Action onCancel,
        CancellationToken ct)
    {
        if (XamlRoot is null)
        {
            return Task.FromResult<IOperationProgressDialog>(new NullOperationProgressDialog());
        }

        var operationNameText = new TextBlock
        {
            Text = GetOperationName(operationType),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        var progressText = new TextBlock { Text = "Preparing operation..." };
        var statusText = new TextBlock
        {
            Text = $"{GetOperationName(operationType)} in progress",
            TextWrapping = TextWrapping.Wrap,
        };
        var currentItemText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
        };
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 6,
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        cancelButton.Click += (_, _) =>
        {
            cancelButton.IsEnabled = false;
            onCancel();
        };

        var content = new Grid
        {
            MinWidth = 480,
            RowSpacing = 8,
            ColumnSpacing = 12,
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        header.Children.Add(operationNameText);
        header.Children.Add(progressText);
        Grid.SetRow(header, 0);
        Grid.SetColumn(header, 0);

        Grid.SetRow(cancelButton, 0);
        Grid.SetColumn(cancelButton, 1);

        Grid.SetRow(progressBar, 1);
        Grid.SetColumn(progressBar, 0);
        Grid.SetColumnSpan(progressBar, 2);

        var details = new StackPanel { Spacing = 2 };
        details.Children.Add(statusText);
        details.Children.Add(currentItemText);
        Grid.SetRow(details, 2);
        Grid.SetColumn(details, 0);
        Grid.SetColumnSpan(details, 2);

        content.Children.Add(header);
        content.Children.Add(cancelButton);
        content.Children.Add(progressBar);
        content.Children.Add(details);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Operation Progress",
            Content = content,
        };

        var closedTaskSource = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        dialog.Closed += (_, _) => closedTaskSource.TrySetResult(null);
        _ = dialog.ShowAsync();
        return Task.FromResult<IOperationProgressDialog>(
            new ContentDialogOperationProgressDialog(
                dialog,
                closedTaskSource.Task,
                progressText,
                statusText,
                currentItemText,
                progressBar));
    }

    public async Task<CollisionPolicy> ShowCollisionDialogAsync(
        NormalizedPath sourcePath, NormalizedPath destinationPath, CancellationToken ct)
    {
        if (XamlRoot is null)
        {
            return CollisionPolicy.Cancel;
        }

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
        {
            return CollisionPolicy.Cancel;
        }

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

    public async Task ShowOperationResultAsync(OperationSummary summary, CancellationToken ct)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Operation: {summary.Type}");
        sb.AppendLine($"Status: {summary.Status}");
        sb.AppendLine($"Duration: {summary.Duration.TotalSeconds:F1}s");
        sb.AppendLine();
        sb.AppendLine($"Total: {summary.TotalItems}  |  Succeeded: {summary.SucceededCount}  |  Failed: {summary.FailedCount}  |  Skipped: {summary.SkippedCount}");

        if (summary.WasCancelled)
        {
            sb.AppendLine("Operation was cancelled.");
        }

        var failedItems = summary.ItemResults.Where(static r => !r.Succeeded).ToList();
        if (failedItems.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Failed items:");
            foreach (var item in failedItems.Take(20))
            {
                sb.AppendLine($"  {item.SourcePath.DisplayPath}");
                if (item.Error is not null)
                {
                    sb.AppendLine($"    {item.Error.Message}");
                }
            }

            if (failedItems.Count > 20)
            {
                sb.AppendLine($"  ... and {failedItems.Count - 20} more");
            }
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

    private static string GetOperationName(OperationType type) =>
        type switch
        {
            OperationType.Copy => "Copy",
            OperationType.Move => "Move",
            OperationType.Delete => "Delete",
            OperationType.CreateFolder => "Create Folder",
            OperationType.Rename => "Rename",
            _ => type.ToString(),
        };

    private sealed class NullOperationProgressDialog : IOperationProgressDialog
    {
        public void ReportProgress(OperationProgressEvent progressEvent)
        {
        }

        public Task CloseAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class ContentDialogOperationProgressDialog(
        ContentDialog dialog,
        Task showTask,
        TextBlock progressText,
        TextBlock statusText,
        TextBlock currentItemText,
        ProgressBar progressBar) : IOperationProgressDialog
    {
        private bool _isClosed;

        public void ReportProgress(OperationProgressEvent progressEvent)
        {
            progressText.Text = progressEvent.TotalItems > 0
                ? $"Processed {progressEvent.CompletedItems} of {progressEvent.TotalItems} items"
                : "Preparing operation...";
            statusText.Text = progressEvent.StatusMessage ?? $"{GetOperationName(progressEvent.Type)} in progress";
            currentItemText.Text = progressEvent.CurrentItemPath?.DisplayPath ?? string.Empty;
            progressBar.Value = progressEvent.TotalItems > 0
                ? (double)progressEvent.CompletedItems / progressEvent.TotalItems * 100.0
                : 0.0;
        }

        public async Task CloseAsync(CancellationToken ct)
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            dialog.Hide();
            await showTask;
        }
    }
}
