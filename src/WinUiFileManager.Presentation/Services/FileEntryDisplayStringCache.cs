using System.Collections.Concurrent;

namespace WinUiFileManager.Presentation.Services;

public sealed class FileEntryDisplayStringCache
{
    private const int MaxCachedExtensions = 512;

    public static FileEntryDisplayStringCache Shared { get; } = new();

    private readonly ConcurrentDictionary<string, string> _extensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<FileAttributes, string> _inspectorAttributes = new();
    private readonly ConcurrentDictionary<FileAttributes, string> _tableAttributes = new();

    private FileEntryDisplayStringCache()
    {
    }

    public string GetExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return string.Empty;
        }

        if (!CanCacheExtension(extension))
        {
            return extension;
        }

        if (_extensions.TryGetValue(extension, out var cached))
        {
            return cached;
        }

        return _extensions.Count < MaxCachedExtensions
            ? _extensions.GetOrAdd(extension, static value => value)
            : extension;
    }

    public string GetInspectorAttributes(FileAttributes attributes) =>
        _inspectorAttributes.GetOrAdd(attributes, static value => value.ToString());

    public string GetTableAttributes(FileAttributes attributes) =>
        _tableAttributes.GetOrAdd(attributes, FormatTableAttributes);

    private static bool CanCacheExtension(string extension)
    {
        if (extension.Length is < 2 or > 5 || extension[0] != '.')
        {
            return false;
        }

        return extension[1..].All(static character => char.IsLetter(character));
    }

    private static string FormatTableAttributes(FileAttributes attributes)
    {
        var builder = new StringBuilder();
        Append(builder, attributes.HasFlag(FileAttributes.Directory) ? "D" : "F");
        AppendIf(builder, attributes, FileAttributes.Hidden, "H");
        AppendIf(builder, attributes, FileAttributes.ReadOnly, "R");
        AppendIf(builder, attributes, FileAttributes.System, "S");
        AppendIf(builder, attributes, FileAttributes.Archive, "A");
        AppendIf(builder, attributes, FileAttributes.Temporary, "T");
        AppendIf(builder, attributes, FileAttributes.Offline, "O");
        AppendIf(builder, attributes, FileAttributes.Compressed, "C");
        AppendIf(builder, attributes, FileAttributes.Encrypted, "E");
        AppendIf(builder, attributes, FileAttributes.NotContentIndexed, "I");
        AppendIf(builder, attributes, FileAttributes.SparseFile, "P");
        AppendIf(builder, attributes, FileAttributes.ReparsePoint, "L");
        return builder.ToString();
    }

    private static void AppendIf(StringBuilder builder, FileAttributes attributes, FileAttributes flag, string shortcut)
    {
        if (attributes.HasFlag(flag))
        {
            Append(builder, shortcut);
        }
    }

    private static void Append(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(value);
    }
}
