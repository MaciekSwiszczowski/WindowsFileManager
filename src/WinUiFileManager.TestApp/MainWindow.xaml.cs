using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;
using WinUiFileManager.Presentation.ViewModels;

namespace WinUiFileManager.TestApp;

public sealed partial class MainWindow : Window
{
    private readonly Grid _layoutRoot = new();
    private readonly SpecFileEntryTableView _leftTable;
    private readonly SpecFileEntryTableView _rightTable;

    public MainWindow()
    {
        Title = "FileEntry Table TestApp";
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1360, 720));
        ApplyTitleBarTheme();
        _leftTable = new SpecFileEntryTableView { Identity = "Left" };
        _rightTable = new SpecFileEntryTableView { Identity = "Right" };
        BuildLayout();

        _leftTable.ItemsSource = LeftEntries;
        _rightTable.ItemsSource = RightEntries;
        _leftTable.CurrentItem = LeftEntries.Count > 0 ? LeftEntries[0] : null;
        _rightTable.CurrentItem = RightEntries.Count > 0 ? RightEntries[0] : null;
        Content = _layoutRoot;
        _layoutRoot.PreviewKeyDown += Window_PreviewKeyDown;
    }

    public ObservableCollection<FileEntryViewModel> LeftEntries { get; } =
    [
        CreateFolder("Documents", "Left", DateTime.UtcNow.AddDays(-2), FileAttributes.Directory),
        CreateFolder("Downloads", "Left", DateTime.UtcNow.AddHours(-6), FileAttributes.Directory),
        CreateFolder("Projects", "Left", DateTime.UtcNow.AddMinutes(-30), FileAttributes.Directory | FileAttributes.Archive),
        CreateFile("readme", "md", "Left", 12_842, DateTime.UtcNow.AddDays(-1), FileAttributes.Archive),
        CreateFile("presentation", "pptx", "Left", 2_902_100, DateTime.UtcNow.AddDays(-9), FileAttributes.Archive),
        CreateFile("large-backup", "zip", "Left", 612_004_221, DateTime.UtcNow.AddMonths(-1), FileAttributes.Archive),
    ];

    public ObservableCollection<FileEntryViewModel> RightEntries { get; } =
    [
        CreateFolder("Desktop", "Right", DateTime.UtcNow.AddDays(-3), FileAttributes.Directory),
        CreateFolder("Pictures", "Right", DateTime.UtcNow.AddHours(-8), FileAttributes.Directory),
        CreateFolder("Scratch", "Right", DateTime.UtcNow.AddMinutes(-12), FileAttributes.Directory),
        CreateFile("invoice", "pdf", "Right", 114_820, DateTime.UtcNow.AddDays(-5), FileAttributes.Archive),
        CreateFile("notes", string.Empty, "Right", 914, DateTime.UtcNow.AddHours(-12), FileAttributes.Archive),
        CreateFile("system-file", "dat", "Right", 32_768, DateTime.UtcNow.AddYears(-1), FileAttributes.Hidden | FileAttributes.System),
    ];

    private void Window_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Up:
                WeakReferenceMessenger.Default.Send(new MoveCursorUpMessage());
                e.Handled = true;
                break;
            case VirtualKey.Down:
                WeakReferenceMessenger.Default.Send(new MoveCursorDownMessage());
                e.Handled = true;
                break;
            case VirtualKey.Home:
                WeakReferenceMessenger.Default.Send(new MoveCursorHomeMessage());
                e.Handled = true;
                break;
            case VirtualKey.End:
                WeakReferenceMessenger.Default.Send(new MoveCursorEndMessage());
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                WeakReferenceMessenger.Default.Send(new ActivateInvokedMessage());
                e.Handled = true;
                break;
            case VirtualKey.A:
                WeakReferenceMessenger.Default.Send(new SelectAllMessage());
                e.Handled = true;
                break;
            case VirtualKey.Escape:
                WeakReferenceMessenger.Default.Send(new ClearSelectionMessage());
                e.Handled = true;
                break;
        }
    }

    private void BuildLayout()
    {
        _layoutRoot.Padding = new Thickness(12);
        _layoutRoot.ColumnSpacing = 12;
        _layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        _layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        _layoutRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(480) });

        var leftPanel = CreatePanel("Left panel", @"C:\FileEntryTableTest\Left", _leftTable);
        var rightPanel = CreatePanel("Right panel", @"C:\FileEntryTableTest\Right", _rightTable);
        var logger = new FileTableMessageLogView();

        Grid.SetColumn(rightPanel, 1);
        Grid.SetColumn(logger, 2);

        _layoutRoot.Children.Add(leftPanel);
        _layoutRoot.Children.Add(rightPanel);
        _layoutRoot.Children.Add(logger);
    }

    private static Grid CreatePanel(string title, string path, SpecFileEntryTableView table)
    {
        var panel = new Grid { RowSpacing = 6 };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBlock = new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };

        var pathBlock = new TextBlock
        {
            Text = path,
            Foreground = ResolveBrush("SystemControlForegroundBaseMediumBrush"),
        };

        Grid.SetRow(pathBlock, 1);
        Grid.SetRow(table, 2);

        panel.Children.Add(titleBlock);
        panel.Children.Add(pathBlock);
        panel.Children.Add(table);

        return panel;
    }

    private static Brush? ResolveBrush(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            && resource is Brush brush)
        {
            return brush;
        }

        return null;
    }

    private static FileEntryViewModel CreateFolder(
        string name,
        string panel,
        DateTime modifiedUtc,
        FileAttributes attributes) =>
        CreateEntry(name, string.Empty, panel, ItemKind.Directory, 0, modifiedUtc, attributes);

    private static FileEntryViewModel CreateFile(
        string name,
        string extension,
        string panel,
        long size,
        DateTime modifiedUtc,
        FileAttributes attributes) =>
        CreateEntry(
            string.IsNullOrEmpty(extension) ? name : $"{name}.{extension}",
            extension,
            panel,
            ItemKind.File,
            size,
            modifiedUtc,
            attributes);

    private static FileEntryViewModel CreateEntry(
        string name,
        string extension,
        string panel,
        ItemKind kind,
        long size,
        DateTime modifiedUtc,
        FileAttributes attributes)
    {
        var model = new FileSystemEntryModel(
            NormalizedPath.FromUserInput(Path.Combine(@"C:\FileEntryTableTest", panel, name)),
            name,
            extension,
            kind,
            size,
            modifiedUtc,
            modifiedUtc.AddDays(-14),
            attributes);

        return new FileEntryViewModel(model);
    }

    private void ApplyTitleBarTheme()
    {
        var titleBar = AppWindow.TitleBar;
        var bg = global::Windows.UI.Color.FromArgb(255, 32, 32, 32);
        var hoverBg = global::Windows.UI.Color.FromArgb(255, 51, 51, 51);
        var pressedBg = global::Windows.UI.Color.FromArgb(255, 70, 70, 70);
        var inactiveFg = global::Windows.UI.Color.FromArgb(255, 153, 153, 153);

        titleBar.BackgroundColor = bg;
        titleBar.ForegroundColor = Colors.White;
        titleBar.InactiveBackgroundColor = bg;
        titleBar.InactiveForegroundColor = inactiveFg;
        titleBar.ButtonBackgroundColor = bg;
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonHoverForegroundColor = Colors.White;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
        titleBar.ButtonPressedForegroundColor = Colors.White;
        titleBar.ButtonInactiveBackgroundColor = bg;
        titleBar.ButtonInactiveForegroundColor = inactiveFg;
    }
}
