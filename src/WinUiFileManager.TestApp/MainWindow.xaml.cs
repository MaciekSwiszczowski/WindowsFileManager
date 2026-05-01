using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Presentation.FileEntryTable;
using WinUiFileManager.Presentation.FileEntryTable.Messages;
using WinUiFileManager.Presentation.Keyboard;

namespace WinUiFileManager.TestApp;

public sealed partial class MainWindow
{
    public KeyboardManager KeyboardManager { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        LeftTable.Identity = "Left";
        RightTable.Identity = "Right";
        LeftTable.ItemsSource = LeftEntries;
        RightTable.ItemsSource = RightEntries;

        var messenger = WeakReferenceMessenger.Default;
        var layout = ColumnLayout.Default;
        messenger.Send(new FileTableParentEntryVisibilityMessage("Left", ShowParentEntry: true));
        messenger.Send(new FileTableParentEntryVisibilityMessage("Right", ShowParentEntry: true));
        messenger.Send(new FileTableColumnLayoutMessage("Left", layout));
        messenger.Send(new FileTableColumnLayoutMessage("Right", layout));
    }

    public ObservableCollection<SpecFileEntryViewModel> LeftEntries { get; } =
    [
        CreateFolder("Documents", "Left", DateTime.UtcNow.AddDays(-2), FileAttributes.Directory),
        CreateFolder("Downloads", "Left", DateTime.UtcNow.AddHours(-6), FileAttributes.Directory),
        CreateFolder("Projects", "Left", DateTime.UtcNow.AddMinutes(-30), FileAttributes.Directory | FileAttributes.Archive),
        CreateFile("readme", "md", "Left", 12_842, DateTime.UtcNow.AddDays(-1), FileAttributes.Archive),
        CreateFile("presentation", "pptx", "Left", 2_902_100, DateTime.UtcNow.AddDays(-9), FileAttributes.Archive),
        CreateFile("large-backup", "zip", "Left", 612_004_221, DateTime.UtcNow.AddMonths(-1), FileAttributes.Archive),
        new SpecFileEntryViewModel(),
    ];

    public ObservableCollection<SpecFileEntryViewModel> RightEntries { get; } =
    [
        CreateFolder("Desktop", "Right", DateTime.UtcNow.AddDays(-3), FileAttributes.Directory),
        CreateFolder("Pictures", "Right", DateTime.UtcNow.AddHours(-8), FileAttributes.Directory),
        CreateFolder("Scratch", "Right", DateTime.UtcNow.AddMinutes(-12), FileAttributes.Directory),
        CreateFile("invoice", "pdf", "Right", 114_820, DateTime.UtcNow.AddDays(-5), FileAttributes.Archive),
        CreateFile("notes", string.Empty, "Right", 914, DateTime.UtcNow.AddHours(-12), FileAttributes.Archive),
        CreateFile("system-file", "dat", "Right", 32_768, DateTime.UtcNow.AddYears(-1), FileAttributes.Hidden | FileAttributes.System),
    ];

    private static SpecFileEntryViewModel CreateFolder(
        string name,
        string panel,
        DateTime modifiedUtc,
        FileAttributes attributes) =>
        CreateEntry(name, string.Empty, panel, ItemKind.Directory, 0, modifiedUtc, attributes);

    private static SpecFileEntryViewModel CreateFile(
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

    private static SpecFileEntryViewModel CreateEntry(
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

        return new SpecFileEntryViewModel(model);
    }
}
