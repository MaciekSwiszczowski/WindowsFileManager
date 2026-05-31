namespace WinUiFileManager.Application.FileEntries;

/// <summary>
/// The canonical path value object for the application layer (see AGENTS.md §7).
/// </summary>
/// <remarks>
/// <para>
/// Internally the path is stored in the Win32 extended-length form (prefixed with
/// <c>\\?\</c>), which lets the rest of the app work with paths longer than
/// <c>MAX_PATH</c> without per-call conversions. <see cref="DisplayPath"/> strips that
/// prefix for anything user-facing (UI, logging).
/// </para>
/// <para>
/// Equality is <b>case-insensitive ordinal</b> over the stored <see cref="Value"/>, matching
/// NTFS path-comparison semantics; <see cref="GetHashCode"/> is consistent with that.
/// </para>
/// <para>
/// This is a <see langword="struct"/>, so the parameterless default leaves
/// <see cref="Value"/> as <see langword="null"/>; the accessors below tolerate that case.
/// </para>
/// </remarks>
public readonly struct NormalizedPath : IEquatable<NormalizedPath>
{
    /// <summary>
    /// Wraps an already-formed path string verbatim. Callers are responsible for the string
    /// being in the expected (typically extended-length) form; use
    /// <see cref="FromUserInput"/> or <see cref="FromFullyQualifiedPath"/> to construct one safely.
    /// </summary>
    public NormalizedPath(string value)
    {
        Value = value;
    }

    private const string ExtendedPathPrefix = @"\\?\";

    /// <summary>The raw stored path, including the <c>\\?\</c> extended-length prefix when present.</summary>
    public string Value { get; }

    /// <summary>
    /// The path with the <c>\\?\</c> prefix stripped, for UI and logging.
    /// </summary>
    /// <remarks>
    /// Hot path: this is read by every row binding/key/log line and allocates a new substring on
    /// each access (see AGENTS.md §3). Callers on the row hot path should memoize rather than recompute.
    /// </remarks>
    public string DisplayPath =>
        Value is not null && Value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? Value[ExtendedPathPrefix.Length..]
            : Value ?? string.Empty;

    /// <summary>
    /// Normalizes a user- or programmatically-typed path: trims whitespace, adds the
    /// extended-length prefix when the input is fully qualified, drops trailing separators, and
    /// re-appends a separator for bare drive roots (e.g. <c>C:</c> → <c>C:\</c>).
    /// </summary>
    /// <param name="path">The path to normalize. Must not be null/empty/whitespace.</param>
    /// <returns>A <see cref="NormalizedPath"/> wrapping the cleaned-up value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
    public static NormalizedPath FromUserInput(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var trimmed = path.Trim();

        if (!trimmed.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            && Path.IsPathFullyQualified(trimmed))
        {
            trimmed = ExtendedPathPrefix + trimmed;
        }

        trimmed = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // A bare drive root collapses to "\\?\C:" after the trim above, but Win32 treats
        // "C:" as the *current* directory on that drive, so restore the trailing separator.
        if (trimmed.Length == ExtendedPathPrefix.Length + 2 && trimmed[^1] == ':')
        {
            trimmed += Path.DirectorySeparatorChar;
        }

        return new NormalizedPath(trimmed);
    }

    /// <summary>
    /// Wraps a path that is already fully qualified, adding the <c>\\?\</c> prefix if missing.
    /// Prefer this over <see cref="FromUserInput"/> when the caller already guarantees a
    /// qualified path, since it skips the trimming/normalization work.
    /// </summary>
    /// <param name="path">An already fully-qualified path. Must not be null/empty/whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
    public static NormalizedPath FromFullyQualifiedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? new NormalizedPath(path)
            : new NormalizedPath(ExtendedPathPrefix + path);
    }

    /// <summary>Case-insensitive ordinal comparison of the underlying <see cref="Value"/>.</summary>
    public bool Equals(NormalizedPath other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) =>
        obj is NormalizedPath other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    public static bool operator ==(NormalizedPath left, NormalizedPath right) => left.Equals(right);

    public static bool operator !=(NormalizedPath left, NormalizedPath right) => !left.Equals(right);

    // String overloads let callers compare against raw user input (e.g. an address bar) without
    // first constructing a NormalizedPath; the right-hand string is normalized before comparison.
    public static bool operator ==(NormalizedPath left, string? right) => Equals(left, right);

    public static bool operator !=(NormalizedPath left, string? right) => !Equals(left, right);

    public static bool operator ==(string? left, NormalizedPath right) => Equals(right, left);

    public static bool operator !=(string? left, NormalizedPath right) => !Equals(right, left);

    /// <summary>Combines this path with a child file/folder <paramref name="name"/> using the stored extended-length value.</summary>
    public NormalizedPath GetChildPath(string name) => new(Path.Combine(Value, name));

    /// <summary>Returns the raw stored <see cref="Value"/> (extended-length form), not <see cref="DisplayPath"/>.</summary>
    public override string ToString() => Value;

    /// <summary>
    /// Compares a normalized path against a raw string. Fully-qualified/extended-length strings are
    /// normalized via <see cref="FromUserInput"/> before comparison; otherwise the string is treated
    /// as a display path and compared against <see cref="DisplayPath"/>. Null/whitespace never matches.
    /// </summary>
    private static bool Equals(NormalizedPath left, string? right)
    {
        if (string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var trimmed = right.Trim();
        if (Path.IsPathFullyQualified(trimmed)
            || trimmed.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal))
        {
            return left == FromUserInput(trimmed);
        }

        var displayPath = trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(left.DisplayPath, displayPath, StringComparison.OrdinalIgnoreCase);
    }
}
