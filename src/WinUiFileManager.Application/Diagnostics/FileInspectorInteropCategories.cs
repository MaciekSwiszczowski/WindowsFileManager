namespace WinUiFileManager.Application.Diagnostics;

[Flags]
public enum FileInspectorInteropCategories
{
    None = 0,
    Identity = 1 << 0,
    Locks = 1 << 1,
    Links = 1 << 2,
    Streams = 1 << 3,
    Security = 1 << 4,
    Thumbnails = 1 << 5,
    Cloud = 1 << 6,
    All = Identity | Locks | Links | Streams | Security | Thumbnails | Cloud
}
