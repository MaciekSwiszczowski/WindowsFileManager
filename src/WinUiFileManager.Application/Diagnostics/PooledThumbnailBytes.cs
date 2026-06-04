using System.Buffers;

namespace WinUiFileManager.Application.Diagnostics;

/// <summary>
/// Owns a rented byte array containing encoded thumbnail bytes that travel from Diagnostics to Presentation.
/// </summary>
/// <remarks>
/// Thumbnail extraction produces large transient buffers in the Diagnostics layer, but Presentation is the layer
/// that consumes those bytes to build the UI image. This owner makes that cross-component lifetime explicit:
/// the producer fills the buffer, the consumer reads <see cref="Memory"/>/<see cref="Segment"/>, and the response
/// lifecycle disposes the owner to return the array to its <see cref="ArrayPool{T}"/>.
/// <para>
/// Ownership is single-owner: exactly one consumer reads then disposes the instance. The bytes are invalid after
/// disposal. <see cref="Dispose"/> returns the array to the pool exactly once, even under concurrent calls.
/// </para>
/// </remarks>
public sealed class PooledThumbnailBytes : IDisposable
{
    private readonly ArrayPool<byte> _pool;
    private byte[]? _buffer;

    private PooledThumbnailBytes(ArrayPool<byte> pool, byte[] buffer)
    {
        _pool = pool;
        _buffer = buffer;
    }

    /// <summary>Number of initialized bytes available to consumers.</summary>
    public int Length { get; private set; }

    /// <summary>Initialized thumbnail bytes. Accessing this after disposal throws.</summary>
    public ReadOnlyMemory<byte> Memory => Buffer.AsMemory(0, Length);

    /// <summary>Initialized thumbnail bytes as an array segment for APIs that require array-backed buffers.</summary>
    public ArraySegment<byte> Segment => new(Buffer, 0, Length);

    /// <summary>Rents storage with at least <paramref name="minimumCapacity"/> bytes.</summary>
    /// <param name="minimumCapacity">Minimum number of bytes the producer expects to write.</param>
    /// <param name="pool">Pool to rent from and return to; defaults to <see cref="ArrayPool{T}.Shared"/>.</param>
    /// <returns>An owner that must be disposed after the consumer has finished reading the bytes.</returns>
    public static PooledThumbnailBytes Rent(int minimumCapacity, ArrayPool<byte>? pool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumCapacity);
        var effectivePool = pool ?? ArrayPool<byte>.Shared;
        return new PooledThumbnailBytes(effectivePool, effectivePool.Rent(minimumCapacity));
    }

    /// <summary>
    /// Reads from <paramref name="source"/> into the rented buffer until it ends or the buffer is full,
    /// then commits the number of initialized bytes.
    /// </summary>
    /// <param name="source">Producer stream positioned at the first byte to copy.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    public async ValueTask FillFromAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var buffer = Buffer;

        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var read = await source
                .ReadAsync(buffer.AsMemory(bytesRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        Length = bytesRead;
    }

    public void Dispose()
    {
        var buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is null)
        {
            return;
        }

        Length = 0;
        _pool.Return(buffer);
    }

    private byte[] Buffer =>
        _buffer ?? throw new ObjectDisposedException(nameof(PooledThumbnailBytes));
}
