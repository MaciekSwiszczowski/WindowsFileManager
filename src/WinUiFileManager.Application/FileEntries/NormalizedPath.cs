namespace WinUiFileManager.Application.FileEntries;

public readonly struct NormalizedPath : IEquatable<NormalizedPath>
{
    public NormalizedPath(string value)
    {
        Value = value;
    }

    private const string ExtendedPathPrefix = @"\\?\";

    public string Value { get; }

    public string DisplayPath =>
        Value is not null && Value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? Value[ExtendedPathPrefix.Length..]
            : Value ?? string.Empty;

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

        if (trimmed.Length == ExtendedPathPrefix.Length + 2 && trimmed[^1] == ':')
        {
            trimmed += Path.DirectorySeparatorChar;
        }

        return new NormalizedPath(trimmed);
    }

    public static NormalizedPath FromFullyQualifiedPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? new NormalizedPath(path)
            : new NormalizedPath(ExtendedPathPrefix + path);
    }

    public bool Equals(NormalizedPath other) =>
        string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) =>
        obj is NormalizedPath other && Equals(other);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    public static bool operator ==(NormalizedPath left, NormalizedPath right) => left.Equals(right);

    public static bool operator !=(NormalizedPath left, NormalizedPath right) => !left.Equals(right);

    public static bool operator ==(NormalizedPath left, string? right) => Equals(left, right);

    public static bool operator !=(NormalizedPath left, string? right) => !Equals(left, right);

    public static bool operator ==(string? left, NormalizedPath right) => Equals(right, left);

    public static bool operator !=(string? left, NormalizedPath right) => !Equals(right, left);

    public NormalizedPath GetChildPath(string name) => new(Path.Combine(Value, name));

    public override string ToString() => Value;

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
