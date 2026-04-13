using WinUI.TableView;
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
}
