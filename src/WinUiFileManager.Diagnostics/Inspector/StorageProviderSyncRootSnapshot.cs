namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Managed snapshot of the sync-root fields the cloud diagnostics handler needs. Keeping snapshots rather
/// than <c>StorageProviderSyncRootInfo</c> WinRT objects prevents the cache from retaining native WinRT
/// state between diagnostics runs.
/// </summary>
/// <param name="Path">Filesystem path of the registered sync root.</param>
/// <param name="Id">Provider registration id for the sync root.</param>
/// <param name="ProviderId">Provider identifier formatted for display.</param>
internal readonly record struct StorageProviderSyncRootSnapshot(string Path, string Id, string ProviderId);
