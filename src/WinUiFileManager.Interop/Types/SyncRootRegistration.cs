namespace WinUiFileManager.Interop.Types;

/// <summary>
/// Managed projection of a single registered Windows cloud sync root, read from the <c>SyncRootManager</c>
/// registry hive by <see cref="Adapters.ISyncRootRegistryReader"/>.
/// </summary>
/// <remarks>
/// Carrying only these three strings — rather than a WinRT <c>StorageProviderSyncRootInfo</c> — is deliberate:
/// the WinRT enumeration allocates COM-backed objects (sync-root infos plus icon/stream references) whose native
/// memory is reclaimed only on GC finalization, so repeated querying accumulates native memory. A plain record
/// has no native footprint to leak.
/// </remarks>
/// <param name="Path">Local filesystem path of the registered sync root (from its <c>UserSyncRoots</c> entry).</param>
/// <param name="Id">Full sync-root registration id (the registry subkey name).</param>
/// <param name="ProviderId">
/// Provider identifier: the segment of <paramref name="Id"/> before the first <c>!</c>, per the documented
/// <c>provider-id!security-id!account-id</c> sync-root id format (e.g. <c>OneDrive</c>).
/// </param>
public readonly record struct SyncRootRegistration(string Path, string Id, string ProviderId);
