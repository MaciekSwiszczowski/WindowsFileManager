namespace WinUiFileManager.Domain.ValueObjects;

public sealed record DirectoryPath
{
    public DirectoryPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
    }

    public string Value { get; }

    public string DisplayPath => new NormalizedPath(Value).DisplayPath;

    public static DirectoryPath FromNormalizedPath(NormalizedPath path) => new(path.Value);

    public static DirectoryPath FromUserInput(string path) => FromNormalizedPath(
        NormalizedPath.FromUserInput(path));

    public static DirectoryPath FromFullyQualifiedPath(string path) => FromNormalizedPath(
        NormalizedPath.FromFullyQualifiedPath(path));

    public static DirectoryPath FromEntryPath(NormalizedPath fullPath)
    {
        var parentPath = Path.GetDirectoryName(fullPath.DisplayPath);

        return string.IsNullOrWhiteSpace(parentPath)
            ? FromNormalizedPath(fullPath)
            : FromUserInput(parentPath);
    }

    public NormalizedPath GetEntryPath(string name) => new(Path.Combine(Value, name));

    public override string ToString() => Value;
}
