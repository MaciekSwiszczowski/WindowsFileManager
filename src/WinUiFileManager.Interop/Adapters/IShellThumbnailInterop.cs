using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Abstraction over the Win32 Shell imaging path for extracting a file/folder thumbnail directly from a path,
/// without acquiring a WinRT <c>StorageFile</c>/<c>StorageFolder</c>. Implemented by
/// <see cref="ShellThumbnailInterop"/>. Exists so higher layers can request a thumbnail with deterministic COM
/// lifetime (and far fewer runtime-callable wrappers) than the WinRT thumbnail API, without referencing
/// <c>Windows.Win32.*</c> directly.
/// </summary>
/// <remarks>
/// Threading: implementations call apartment-agnostic Shell COM and are intended to run off the UI thread (the
/// extraction is synchronous and can block on slow handlers). The managed thread must have COM initialized, which
/// the CLR does by default (MTA) for thread-pool and benchmark threads.
/// </remarks>
public interface IShellThumbnailInterop
{
    /// <summary>
    /// Extracts a square thumbnail (up to <paramref name="size"/> pixels) for <paramref name="path"/> and copies it
    /// into a managed BGRA pixel buffer.
    /// </summary>
    /// <param name="path">Display (parsing) path of the target file or folder, not the <c>\\?\</c> form.</param>
    /// <param name="size">Requested edge length in pixels; the Shell resizes to fit this box.</param>
    /// <param name="thumbnail">On success, the decoded BGRA thumbnail; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if a thumbnail was produced; <see langword="false"/> on any failure.</returns>
    public bool TryGetThumbnail(string path, uint size, out ShellThumbnail thumbnail);
}
