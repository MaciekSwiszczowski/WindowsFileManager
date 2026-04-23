namespace WinUiFileManager.Domain.ValueObjects;

public readonly record struct NormalizedPath
{
    public NormalizedPath(string value)
    {
        Value = value;
    }

    private const string ExtendedPathPrefix = @"\\?\";

    public string Value { get; init; }

    public string DisplayPath =>
        Value.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            ? Value[ExtendedPathPrefix.Length..]
            : Value;

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

    public static implicit operator NormalizedPath(string path) => FromUserInput(path);

    public override string ToString() => Value;
}
