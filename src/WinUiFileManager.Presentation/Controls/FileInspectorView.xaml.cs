using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileInspectorView : UserControl
{
    public FileInspectorView()
    {
        InitializeComponent();
    }

    private FileInspectorViewModel? _viewModel;
    public FileInspectorViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            DataContext = value;
        }
    }

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.CopyAllCommand.Execute(null);
    }
}
