namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

/// <summary>
/// View model for the inspector's "Properties" button. Opens the Windows shell properties dialog for the currently
/// inspected item via <see cref="IShellService"/>. The target item is set externally (by the inspector) as the
/// single selection changes; the command no-ops when nothing is selected.
/// </summary>
public sealed class InspectorPropertiesButtonViewModel
{
    private readonly IShellService _shellService;
    private FileSystemEntryModel? _selectedItem;

    public InspectorPropertiesButtonViewModel(IShellService shellService)
    {
        _shellService = shellService;
        ShowPropertiesCommand = new AsyncRelayCommand(ShowPropertiesAsync);
    }

    /// <summary>Command bound to the properties button.</summary>
    public IAsyncRelayCommand ShowPropertiesCommand { get; }

    /// <summary>Sets (or clears, with <c>null</c>) the item whose shell properties dialog the command will open.</summary>
    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedItem = selectedItem;
    }

    /// <summary>
    /// Opens the shell properties dialog for the selected item. No-op when nothing is selected. The shell call is
    /// UI/STA-affine (it shows a dialog); it must be invoked from the UI thread (AGENTS.md §6).
    /// </summary>
    private async Task ShowPropertiesAsync()
    {
        if (_selectedItem is not { } selectedItem)
        {
            return;
        }

        await _shellService.ShowPropertiesAsync(selectedItem.FullPath, CancellationToken.None);
    }
}
