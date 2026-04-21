namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record PaneColumnLayoutDto
{
    public double NameWidth { get; init; }

    public double ExtensionWidth { get; init; }

    public double SizeWidth { get; init; }

    public double ModifiedWidth { get; init; }

    public double AttributesWidth { get; init; }
}
