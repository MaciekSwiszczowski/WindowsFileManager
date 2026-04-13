using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Application.Selection;

public sealed class SelectionSet
{
    private readonly HashSet<NormalizedPath> _selectedPaths = [];
    private readonly List<FileSystemEntryModel> _selectedEntries = [];

    public IReadOnlyList<FileSystemEntryModel> SelectedItems => _selectedEntries.AsReadOnly();

    public int Count => _selectedEntries.Count;

    public void Select(FileSystemEntryModel entry)
    {
        if (_selectedPaths.Add(entry.FullPath))
        {
            _selectedEntries.Add(entry);
        }
    }

    public void Deselect(NormalizedPath path)
    {
        if (_selectedPaths.Remove(path))
        {
            _selectedEntries.RemoveAll(e => e.FullPath == path);
        }
    }

    public void ToggleSelection(FileSystemEntryModel entry)
    {
        if (_selectedPaths.Contains(entry.FullPath))
        {
            Deselect(entry.FullPath);
        }
        else
        {
            Select(entry);
        }
    }

    public void SelectAll(IEnumerable<FileSystemEntryModel> entries)
    {
        foreach (var entry in entries)
        {
            Select(entry);
        }
    }

    public void Clear()
    {
        _selectedPaths.Clear();
        _selectedEntries.Clear();
    }

    public bool IsSelected(NormalizedPath path) => _selectedPaths.Contains(path);
}
