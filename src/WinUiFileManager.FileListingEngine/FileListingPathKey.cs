namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// Case-insensitive, ordinal value-type key wrapping a full file path. Used as the identity for rows in
/// the file table's data-source cache so the same file always maps to a single cache entry regardless
/// of casing.
/// </summary>
/// <remarks>
/// A <see langword="readonly struct"/> to avoid per-row heap allocation given the table can hold many
/// thousands of rows (AGENTS.md §3). Equality, hashing, and comparison all use
/// <see cref="StringComparer.OrdinalIgnoreCase"/> to match Windows path semantics.
/// </remarks>
public readonly struct FileListingPathKey : IEquatable<FileListingPathKey>, IComparable<FileListingPathKey>
{
    private readonly string _value;

    /// <param name="value">The full path to key on; must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    public FileListingPathKey(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(FileListingPathKey other) =>
        StringComparer.OrdinalIgnoreCase.Equals(_value, other._value);

    public override bool Equals(object? obj) =>
        obj is FileListingPathKey other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

    public int CompareTo(FileListingPathKey other) =>
        StringComparer.OrdinalIgnoreCase.Compare(_value, other._value);

    public override string ToString() => _value;
}
