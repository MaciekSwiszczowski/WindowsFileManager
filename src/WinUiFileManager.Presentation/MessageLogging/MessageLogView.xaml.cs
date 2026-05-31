namespace WinUiFileManager.Presentation.MessageLogging;

/// <summary>
/// Code-behind for the diagnostic message-log window: binds to a <see cref="MessageLogStore"/> and wires
/// its toolbar (id filter text box, pause/resume toggle, clear) to the store. All event handlers are
/// declared in XAML and target this view, so there is nothing to unsubscribe.
/// </summary>
public sealed partial class MessageLogView
{
    public MessageLogView(MessageLogStore store)
    {
        Store = store;
        InitializeComponent();
        UpdatePauseButton();
    }

    /// <summary>The backing log store, exposed for x:Bind to its <see cref="MessageLogStore.Entries"/>.</summary>
    public MessageLogStore Store { get; }

    // Push the filter text into the store, which re-filters the displayed entries.
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

    /// <summary>Syncs the pause button's label/checked state with the store's paused flag.</summary>
    private void UpdatePauseButton()
    {
        PauseButton.Content = Store.IsPaused ? "Resume" : "Pause";
        PauseButton.IsChecked = Store.IsPaused;
    }
}
