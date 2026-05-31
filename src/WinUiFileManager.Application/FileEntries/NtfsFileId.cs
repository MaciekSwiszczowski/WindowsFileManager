namespace WinUiFileManager.Application.FileEntries;

/// <summary>
/// Value object wrapping an NTFS file identifier (the 128-bit FILE_ID_128 / file reference number)
/// as an opaque byte sequence, used by the file-diagnostics inspector to surface file identity.
/// </summary>
/// <remarks>
/// Equality and hashing are <b>by byte-sequence content</b>, not by reference, so two ids built from
/// equal byte arrays compare equal. The default/empty value is <see cref="None"/>.
/// </remarks>
public readonly record struct NtfsFileId
{
    /// <summary>The empty/unknown identifier (zero bytes); used when no NTFS id is available.</summary>
    public static readonly NtfsFileId None = new([]);

    private readonly byte[] _bytes = [];

    /// <summary>Wraps the raw identifier bytes. A null array is normalized to an empty (zero-length) id.</summary>
    public NtfsFileId(byte[] bytes)
    {
        _bytes = bytes ?? [];
    }

    /// <summary>The raw identifier bytes, exposed as a span to avoid copying the backing array.</summary>
    public ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>The identifier rendered as an uppercase hex string for display in the inspector.</summary>
    public string HexDisplay => Convert.ToHexString(_bytes);

    /// <summary>Content equality: compares the underlying bytes element-by-element.</summary>
    public bool Equals(NtfsFileId other) => _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <summary>Hashes the byte content so equal ids hash equally (consistent with <see cref="Equals(NtfsFileId)"/>).</summary>
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
