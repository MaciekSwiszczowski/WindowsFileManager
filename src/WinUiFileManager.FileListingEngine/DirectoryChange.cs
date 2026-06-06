namespace WinUiFileManager.FileListingEngine;

/// <summary>
/// A single change notification for a watched directory's contents, emitted by
/// <see cref="IDirectoryChangeStream"/>.
/// </summary>
/// <param name="Kind">What happened to the entry (created/deleted/changed/renamed).</param>
/// <param name="Path">The affected path; for a rename this is the new path.</param>
/// <param name="OldPath">The previous path; populated only when <paramref name="Kind"/> is <see cref="DirectoryChangeKind.Renamed"/>.</param>
public sealed record DirectoryChange(DirectoryChangeKind Kind, string Path, string? OldPath = null);
