using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUiFileManager.Presentation.MessageLogging;

public sealed partial class MessageLogView : UserControl
{
    public MessageLogView()
    {
        InitializeComponent();
        UpdatePauseButton();
    }

    public MessageLogStore Store { get; } = new();

    private void IdFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        Store.IdFilter = IdFilterBox.Text;
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        Store.TogglePaused();
        UpdatePauseButton();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Store.Clear();
    }

    private void UpdatePauseButton()
    {
        PauseButton.Content = Store.IsPaused ? "Resume" : "Pause";
        PauseButton.IsChecked = Store.IsPaused;
    }
}
