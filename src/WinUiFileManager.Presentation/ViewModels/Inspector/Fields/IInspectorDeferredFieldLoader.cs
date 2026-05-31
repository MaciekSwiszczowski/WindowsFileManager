using WinUiFileManager.Presentation.FileEntryTable;

namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public interface IInspectorDeferredFieldLoader : IDisposable
{
    public void Load(SpecFileEntryViewModel selectedItem);

    public void Cancel();
}
