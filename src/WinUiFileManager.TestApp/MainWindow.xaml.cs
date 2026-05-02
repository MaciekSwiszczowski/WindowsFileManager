using System.Reactive.Disposables;
using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.TestApp;

public sealed partial class MainWindow
{
    private readonly FileEntryTableDataSource _leftDataSource;
    private readonly FileEntryTableDataSource _rightDataSource;
    private readonly CompositeDisposable _subscriptions = new();

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

        _subscriptions.Add(_leftDataSource.States
            .Subscribe(ApplyLeftState));
        _subscriptions.Add(_rightDataSource.States
            .Subscribe(ApplyRightState));
        Closed += OnClosed;

        var messenger = WeakReferenceMessenger.Default;
        var layout = ColumnLayout.Default;
        messenger.Send(new FileTableColumnLayoutMessage("Left", layout));
        messenger.Send(new FileTableColumnLayoutMessage("Right", layout));
    }

    private void OnClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        _subscriptions.Dispose();
        _leftDataSource.Dispose();
        _rightDataSource.Dispose();
    }

    private void ApplyLeftState(FileEntryTableDataState state)
    {
        LeftTable.ItemsSource = state.Items;
        LeftPathText.Text = state.CurrentPath;
    }

    private void ApplyRightState(FileEntryTableDataState state)
    {
        RightTable.ItemsSource = state.Items;
        RightPathText.Text = state.CurrentPath;
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
