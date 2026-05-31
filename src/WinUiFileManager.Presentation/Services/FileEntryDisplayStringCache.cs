using System.Collections.Concurrent;

namespace WinUiFileManager.Presentation.Services;

/// <summary>
/// Process-lifetime memoisation cache for the small, highly-repeated display strings derived from file
/// metadata: interned extension strings, the verbose inspector attribute text, and the compact table
/// attribute glyph string. Shared via the <see cref="Shared"/> singleton.
/// </summary>
/// <remarks>
/// <b>Why this exists (AGENTS.md §2/§3):</b> the file table can show ~10k+ rows and the row VM is kept
/// lean with no cached strings, so these display strings are produced on demand by cell templates and
/// converters. Without caching, every row/redraw would re-allocate identical strings (e.g. ".txt",
/// "F R A"). Because the set of distinct extensions and attribute combinations is tiny relative to the
/// row count, caching collapses thousands of allocations down to a handful of shared instances.
/// <para>
/// <b>Bounding:</b> attribute combinations are naturally bounded (a finite set of flag combinations) so
/// those caches are unbounded but effectively small. Extensions are user-controlled, so the extension
/// cache is bounded by <see cref="MaxCachedExtensions"/> and only sensible-looking extensions are cached
/// (see <see cref="CanCacheExtension"/>); past the cap, or for odd extensions, the original string is
/// returned uncached. All three maps are <see cref="ConcurrentDictionary{TKey,TValue}"/> because they
/// are read from both the UI thread (templates) and scan/reader threads.
/// </para>
/// </remarks>
public sealed class FileEntryDisplayStringCache
{
    // Upper bound on distinct extensions cached, since extensions are user-controlled (untrusted size).
    private const int MaxCachedExtensions = 512;

    /// <summary>The shared process-wide instance used by converters and the row factory.</summary>
    public static FileEntryDisplayStringCache Shared { get; } = new();

    private readonly ConcurrentDictionary<string, string> _extensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<FileAttributes, string> _inspectorAttributes = new();
    private readonly ConcurrentDictionary<FileAttributes, string> _tableAttributes = new();

    private FileEntryDisplayStringCache()
    {
    }

    /// <summary>Returns an interned (shared-instance) copy of a file extension so identical extensions
    /// across rows reuse one string. Empty input returns empty; un-cacheable or over-cap extensions are
    /// returned as-is without caching.</summary>
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

        // Stop growing the cache once the cap is hit; uncached extensions still work, just unshared.
        return _extensions.Count < MaxCachedExtensions
            ? _extensions.GetOrAdd(extension, static value => value)
            : extension;
    }

    /// <summary>Returns the verbose attribute text (the framework's <see cref="FileAttributes.ToString"/>)
    /// for the inspector, memoised per attribute combination.</summary>
    public string GetInspectorAttributes(FileAttributes attributes) =>
        _inspectorAttributes.GetOrAdd(attributes, static value => value.ToString());

    /// <summary>Returns the compact, space-separated attribute glyph string (e.g. "F R A") for the file
    /// table's Attributes column, memoised per attribute combination.</summary>
    public string GetTableAttributes(FileAttributes attributes) =>
        _tableAttributes.GetOrAdd(attributes, FormatTableAttributes);

    /// <summary>Gate for the extension cache: only short (2–5 char), dot-prefixed, all-letter extensions
    /// are cached, which keeps out pathological/attacker-controlled long or junk extensions.</summary>
    private static bool CanCacheExtension(string extension)
    {
        if (extension.Length is < 2 or > 5 || extension[0] != '.')
        {
            return false;
        }

        return extension[1..].All(static character => char.IsLetter(character));
    }

    /// <summary>Builds the compact table glyph string: a leading D/F (directory vs file) followed by a
    /// single letter per set attribute flag, space-separated.</summary>
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

    /// <summary>Appends <paramref name="shortcut"/> only when <paramref name="flag"/> is set.</summary>
    private static void AppendIf(StringBuilder builder, FileAttributes attributes, FileAttributes flag, string shortcut)
    {
        if (attributes.HasFlag(flag))
        {
            Append(builder, shortcut);
        }
    }

    /// <summary>Appends a token, inserting a separating space before it when the builder is non-empty.</summary>
    private static void Append(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(value);
    }
}
