using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.TestApp;

public sealed partial class MainWindow
{
    private readonly FileEntryTableDataSource _leftDataSource;
    private readonly FileEntryTableDataSource _rightDataSource;

    public KeyboardManager KeyboardManager { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        LeftTable.Identity = "Left";
        RightTable.Identity = "Right";
        var uiScheduler = new DispatcherQueueScheduler(DispatcherQueue);

        _leftDataSource = new FileEntryTableDataSource(
            "Left",
            ResolveInitialPath(
                @"C:\FileEntryTableTest\Left",
                Environment.SpecialFolder.UserProfile),
            uiScheduler);
        _rightDataSource = new FileEntryTableDataSource(
            "Right",
            ResolveInitialPath(
                @"C:\FileEntryTableTest\Right",
                Environment.SpecialFolder.DesktopDirectory),
            uiScheduler);

        LeftTable.ItemsSource = _leftDataSource.Items;
        RightTable.ItemsSource = _rightDataSource.Items;
        LeftPathText.Text = _leftDataSource.CurrentPath;
        RightPathText.Text = _rightDataSource.CurrentPath;
        _leftDataSource.CurrentPathChanged += OnLeftPathChanged;
        _rightDataSource.CurrentPathChanged += OnRightPathChanged;
        Closed += OnClosed;

        var messenger = WeakReferenceMessenger.Default;
        var layout = ColumnLayout.Default;
        messenger.Send(new FileTableColumnLayoutMessage("Left", layout));
        messenger.Send(new FileTableColumnLayoutMessage("Right", layout));
    }

    private void OnClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        _leftDataSource.CurrentPathChanged -= OnLeftPathChanged;
        _rightDataSource.CurrentPathChanged -= OnRightPathChanged;
        _leftDataSource.Dispose();
        _rightDataSource.Dispose();
    }

    private void OnLeftPathChanged(object? sender, EventArgs args)
    {
        LeftPathText.Text = _leftDataSource.CurrentPath;
    }

    private void OnRightPathChanged(object? sender, EventArgs args)
    {
        RightPathText.Text = _rightDataSource.CurrentPath;
    }

    private static string ResolveInitialPath(
        string preferredPath,
        Environment.SpecialFolder fallbackFolder)
    {
        if (Directory.Exists(preferredPath))
        {
            return preferredPath;
        }

        var fallbackPath = Environment.GetFolderPath(fallbackFolder);
        if (Directory.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (Directory.Exists(userProfile))
        {
            return userProfile;
        }

        return Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    }
}
