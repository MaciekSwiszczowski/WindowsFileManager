namespace WinUiFileManager.Presentation.ViewModels.Inspector.Buttons;

public sealed class InspectorPropertiesButtonViewModel
{
    private readonly IShellService _shellService;
    private FileSystemEntryModel? _selectedItem;

    public InspectorPropertiesButtonViewModel(IShellService shellService)
    {
        _shellService = shellService;
        ShowPropertiesCommand = new AsyncRelayCommand(ShowPropertiesAsync);
    }

    public IAsyncRelayCommand ShowPropertiesCommand { get; }

    public void SetSelectedItem(FileSystemEntryModel? selectedItem)
    {
        _selectedItem = selectedItem;
    }

    private async Task ShowPropertiesAsync()
    {
        if (_selectedItem is not { } selectedItem)
        {
            return;
        }

        await _shellService.ShowPropertiesAsync(selectedItem.FullPath, CancellationToken.None);
    }
}
