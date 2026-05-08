namespace WinUiFileManager.Application.Dialogs;

public sealed record DialogButtonConfiguration(
    DialogButtonRole Role,
    string Text,
    bool IsDefault = false);
