using System.Runtime.InteropServices;

namespace WinUiFileManager.Domain.ValueObjects;

[StructLayout(LayoutKind.Auto)]
public readonly record struct PaneColumnLayout(
    double NameWidth,
    double ExtensionWidth,
    double SizeWidth,
    double ModifiedWidth,
    double AttributesWidth)
{
    public static PaneColumnLayout Default { get; } = new(
        NameWidth: 320d,
        ExtensionWidth: 40d,
        SizeWidth: 70d,
        ModifiedWidth: 120d,
        AttributesWidth: 50d);
}
