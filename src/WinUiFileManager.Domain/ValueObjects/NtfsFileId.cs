namespace WinUiFileManager.Domain.ValueObjects;

public readonly record struct NtfsFileId
{
    public static readonly NtfsFileId None = new([]);

    private readonly byte[] _bytes = [];

    public NtfsFileId(byte[] bytes)
    {
        _bytes = bytes ?? [];
    }

    public ReadOnlySpan<byte> Bytes => _bytes;

    public string HexDisplay => Convert.ToHexString(_bytes);

    public bool Equals(NtfsFileId other) => _bytes.AsSpan().SequenceEqual(other._bytes);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var b in _bytes)
        {
            hash.Add(b);
        }

        return hash.ToHashCode();
    }
}
