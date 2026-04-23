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
            Bindings.Update();
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SearchText = SearchBox.Text;
    }

    private async void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.CopyAllCommand.ExecuteAsync(null);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.RefreshCommand.Execute(null);
    }

    private async void OnPropertiesClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.ShowPropertiesCommand.ExecuteAsync(null);
        }
    }
}
