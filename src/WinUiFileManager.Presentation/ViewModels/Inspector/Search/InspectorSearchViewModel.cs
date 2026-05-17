namespace WinUiFileManager.Presentation.ViewModels.Inspector.Search;

public sealed partial class InspectorSearchViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;
}
