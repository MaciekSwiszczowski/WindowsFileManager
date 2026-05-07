namespace WinUiFileManager.Presentation.Dialogs;

public sealed record DialogButtonConfiguration(
    DialogButtonRole Role,
    string Text,
    bool IsDefault = false);
