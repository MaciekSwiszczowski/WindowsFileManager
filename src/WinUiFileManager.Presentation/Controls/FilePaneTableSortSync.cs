using Microsoft.UI.Xaml;
using WinUI.TableView;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Controls;

internal static class FilePaneTableSortSync
{
    internal static void SyncColumnSortDirections(TableView table, FilePaneViewModel host)
    {
        var dir = host.SortAscending ? WinUI.TableView.SortDirection.Ascending : WinUI.TableView.SortDirection.Descending;
        foreach (var col in table.Columns)
        {
            var path = col.SortMemberPath;
            if (string.IsNullOrEmpty(path))
            {
                col.SortDirection = null;
                continue;
            }

            var mapped = FileEntryTableViewModel.MapSortMemberPath(path);
            col.SortDirection = mapped == host.SortBy ? dir : null;
        }
    }

    internal static void SyncColumnWidths(TableView table, PaneColumnLayout layout)
    {
        foreach (var column in table.Columns)
        {
            var width = ResolveDesiredWidth(column.SortMemberPath, layout);
            if (width > 0)
            {
                column.Width = new GridLength(width, GridUnitType.Pixel);
            }
        }
    }

    internal static PaneColumnLayout CaptureColumnWidths(TableView table, PaneColumnLayout fallback)
    {
        var captured = fallback;
        foreach (var column in table.Columns)
        {
            var actual = column.ActualWidth;
            if (actual <= 0)
            {
                continue;
            }

            captured = column.SortMemberPath switch
            {
                nameof(FileEntryViewModel.Name) => captured with { NameWidth = actual },
                nameof(FileEntryViewModel.Extension) => captured with { ExtensionWidth = actual },
                nameof(FileEntryViewModel.Size) => captured with { SizeWidth = actual },
                nameof(FileEntryViewModel.LastWriteTime) => captured with { ModifiedWidth = actual },
                nameof(FileEntryViewModel.Attributes) => captured with { AttributesWidth = actual },
                _ => captured,
            };
        }

        return captured;
    }

    private static double ResolveDesiredWidth(string? sortMemberPath, PaneColumnLayout layout) =>
        sortMemberPath switch
        {
            nameof(FileEntryViewModel.Name) => layout.NameWidth,
            nameof(FileEntryViewModel.Extension) => layout.ExtensionWidth,
            nameof(FileEntryViewModel.Size) => layout.SizeWidth,
            nameof(FileEntryViewModel.LastWriteTime) => layout.ModifiedWidth,
            nameof(FileEntryViewModel.Attributes) => layout.AttributesWidth,
            _ => 0d,
        };
}
