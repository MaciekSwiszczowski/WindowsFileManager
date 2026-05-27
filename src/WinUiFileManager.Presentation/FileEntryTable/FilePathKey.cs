namespace WinUiFileManager.Presentation.FileEntryTable;

public readonly struct FilePathKey : IEquatable<FilePathKey>, IComparable<FilePathKey>
{
    private readonly string _value;

    public FilePathKey(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public bool Equals(FilePathKey other) =>
        StringComparer.OrdinalIgnoreCase.Equals(_value, other._value);

    public override bool Equals(object? obj) =>
        obj is FilePathKey other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(_value);

    public int CompareTo(FilePathKey other) =>
        StringComparer.OrdinalIgnoreCase.Compare(_value, other._value);

    public override string ToString() => _value;
}
