namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Managed snapshot of the sync-root fields the cloud diagnostics handler needs. This is the cache's own model,
/// mapped from the Interop layer's <c>SyncRootRegistration</c> read DTO; the indirection keeps the cache model
/// free to evolve independently of the registry-read shape. Holding plain strings (rather than
/// <c>StorageProviderSyncRootInfo</c> WinRT objects) keeps the cache free of native provider-registration state.
/// </summary>
/// <param name="Path">Filesystem path of the registered sync root.</param>
/// <param name="Id">Provider registration id for the sync root.</param>
/// <param name="ProviderId">Provider identifier formatted for display.</param>
/// <param name="DisplayName">Friendly provider display name (registry-sourced replacement for <c>StorageProvider.DisplayName</c>).</param>
internal readonly record struct StorageProviderSyncRootSnapshot(string Path, string Id, string ProviderId, string DisplayName);
