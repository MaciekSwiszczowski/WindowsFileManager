using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Enumerates the machine's registered Windows cloud sync roots by reading the <c>SyncRootManager</c> registry
/// hive directly, as a lightweight, allocation-cheap replacement for the WinRT
/// <c>StorageProviderSyncRootManager.GetCurrentSyncRoots()</c> enumeration. Implemented by
/// <see cref="SyncRootRegistryReader"/>; consumed by the cloud diagnostics sync-root cache.
/// </summary>
/// <remarks>
/// Lives in Interop alongside the other Windows platform-query adapters that the diagnostics layer consumes.
/// Unlike the WinRT API it replaces, this returns plain managed records with no native/COM footprint, so
/// repeated enumeration does not accumulate finalizer-reclaimed native memory.
/// </remarks>
public interface ISyncRootRegistryReader
{
    /// <summary>
    /// Reads all registered sync roots for the machine. Best-effort: returns an empty list (never throws) when the
    /// registry hive is absent or inaccessible, which is indistinguishable from "no cloud sync roots are registered".
    /// </summary>
    /// <returns>One entry per registered sync-root path; empty when none are registered or on failure.</returns>
    public IReadOnlyList<SyncRootRegistration> ReadRegisteredSyncRoots();
}
