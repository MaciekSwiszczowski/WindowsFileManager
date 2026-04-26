namespace WinUiFileManager.Presentation.FileEntryTable;

public sealed record ColumnLayout(
    double NameWidth,
    double ExtensionWidth,
    double SizeWidth,
    double ModifiedWidth,
    double AttributesWidth)
{
    public static ColumnLayout Default { get; } = new(320, 40, 70, 120, 50);
}
