namespace WinUiFileManager.Presentation.Controls;

public sealed partial class FileInspectorView
{
    public FileInspectorView()
    {
        InitializeComponent();
    }

    public FileInspectorViewModel? ViewModel
    {
        get;
        set
        {
            field = value;
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
            await ViewModel.Commands.CopyAllCommand.ExecuteAsync(null);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.Commands.RefreshCommand.Execute(null);
    }

    private async void OnPropertiesClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            await ViewModel.Commands.ShowPropertiesCommand.ExecuteAsync(null);
        }
    }
}
