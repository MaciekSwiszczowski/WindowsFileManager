using Microsoft.Win32.SafeHandles;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction for handle-based NTFS metadata queries (timestamps/attributes, reparse tag, modern and legacy
/// file identity, volume serial, final path, alternate data streams). Implemented by
/// <see cref="FileSystemMetadataInterop"/>; consumed by the file-diagnostics layer.
/// </summary>
/// <remarks>
/// Callers own the <see cref="SafeFileHandle"/> returned by <see cref="OpenForMetadataRead"/> and must dispose it;
/// the <c>TryGet*</c> methods borrow a handle without taking ownership. The <c>Try*</c> methods report failure via
/// their return value rather than throwing.
/// </remarks>
public interface IFileSystemMetadataInterop
{
    /// <summary>Opens an owned, metadata-only handle (full sharing). Caller disposes it.</summary>
    /// <param name="path">Target path.</param>
    /// <param name="treatAsDirectory"><see langword="true"/> to open a directory (uses backup semantics).</param>
    /// <exception cref="InvalidOperationException">If the open fails.</exception>
    public SafeFileHandle OpenForMetadataRead(string path, bool treatAsDirectory);

    /// <summary>Reads attributes and the four NTFS timestamps.</summary>
    /// <returns><see langword="true"/> on success.</returns>
    public bool TryGetFileBasicInfo(SafeFileHandle handle, out FileBasicInteropInfo info);

    /// <summary>Reads the reparse tag (identifies link/placeholder kinds).</summary>
    /// <returns><see langword="true"/> on success; <paramref name="reparseTag"/> is <c>0</c> on failure.</returns>
    public bool TryGetFileAttributeReparseTag(SafeFileHandle handle, out uint reparseTag);

    /// <summary>Reads the NTFS 128-bit file identity as a 16-byte array.</summary>
    /// <returns><see langword="true"/> on success; <paramref name="fileId16"/> is <see langword="null"/> on failure.</returns>
    public bool TryGetNtfsFileIdBytes(SafeFileHandle handle, out byte[]? fileId16);

    /// <summary>Reads the legacy 64-bit file index and hard-link count.</summary>
    /// <param name="win32Error">Captured Win32 error on failure; <c>0</c> on success.</param>
    /// <returns><see langword="true"/> on success.</returns>
    public bool TryGetLegacyFileIndex(SafeFileHandle handle, out LegacyFileIndexInfo info, out int win32Error);

    /// <summary>Returns the volume serial as 8 uppercase hex digits, or <see langword="null"/> on failure.</summary>
    /// <param name="volumeRootPath">Volume root (e.g. <c>C:\</c>).</param>
    public string? TryGetVolumeSerialHex(string volumeRootPath);

    /// <summary>Resolves the canonical final path (follows links), or <see langword="null"/> on failure.</summary>
    public string? TryGetFinalPath(SafeFileHandle handle);

    /// <summary>Enumerates alternate data streams as display lines; empty when none. See <see cref="IAlternateDataStreamInterop"/>.</summary>
    public IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path);
}
