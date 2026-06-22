using System.Buffers.Binary;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace WinUiFileManager.Application.Caching;

/// <summary>
/// A 128-bit content fingerprint of a thumbnail's raw bytes, used as the key in
/// <see cref="ThumbnailConversionCache{TImage}"/>. Computed with the non-cryptographic <see cref="XxHash128"/>:
/// a collision would at worst surface one wrong thumbnail, acceptable for a small bounded image cache, so a fast
/// hash is preferred over a cryptographic one.
/// </summary>
/// <remarks>
/// Keying on the <i>content</i> (not the path or extension) is deliberate: identical bytes from different files
/// (e.g. every file of one type sharing an icon) dedupe to one cached image, and a file whose thumbnail changes —
/// including cloud placeholders that can mutate without a path/timestamp signal — produces different bytes and so a
/// different key, so the cache self-invalidates.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct ThumbnailContentHash : IEquatable<ThumbnailContentHash>
{
    private readonly ulong _low;
    private readonly ulong _high;

    private ThumbnailContentHash(ulong low, ulong high)
    {
        _low = low;
        _high = high;
    }

    /// <summary>Computes the 128-bit fingerprint of <paramref name="content"/>.</summary>
    public static ThumbnailContentHash Compute(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[16];
        XxHash128.Hash(content, digest);
        return new ThumbnailContentHash(
            BinaryPrimitives.ReadUInt64LittleEndian(digest),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[8..]));
    }

    /// <summary>
    /// Computes the fingerprint of <paramref name="content"/> together with the <paramref name="width"/> and
    /// <paramref name="height"/> the bytes are interpreted as, so two byte-identical buffers that differ only in
    /// dimensions (e.g. a uniform thumbnail at 48×24 vs 24×48) produce distinct keys.
    /// </summary>
    public static ThumbnailContentHash Compute(ReadOnlySpan<byte> content, int width, int height)
    {
        var hasher = new XxHash128();
        hasher.Append(content);

        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, width);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], height);
        hasher.Append(dimensions);

        Span<byte> digest = stackalloc byte[16];
        hasher.GetHashAndReset(digest);
        return new ThumbnailContentHash(
            BinaryPrimitives.ReadUInt64LittleEndian(digest),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[8..]));
    }

    public bool Equals(ThumbnailContentHash other) => _low == other._low && _high == other._high;

    public override bool Equals(object? obj) => obj is ThumbnailContentHash other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_low, _high);

    public static bool operator ==(ThumbnailContentHash left, ThumbnailContentHash right) => left.Equals(right);

    public static bool operator !=(ThumbnailContentHash left, ThumbnailContentHash right) => !left.Equals(right);
}
