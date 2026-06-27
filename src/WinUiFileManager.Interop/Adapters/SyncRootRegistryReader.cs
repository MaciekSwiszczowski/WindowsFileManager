using Microsoft.Win32;
using Windows.Win32;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Interop.Adapters;

/// <summary>
/// Reads registered Windows cloud sync roots from
/// <c>HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager</c> — the registry hive that backs
/// the Cloud Filter API and the WinRT <c>StorageProviderSyncRootManager</c>. Implements
/// <see cref="ISyncRootRegistryReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// Replaces <c>StorageProviderSyncRootManager.GetCurrentSyncRoots()</c>, which allocates COM-backed WinRT objects
/// (sync-root infos plus icon/stream references) whose native memory is released only by GC finalization; under
/// repeated querying that native footprint accumulates faster than finalization reclaims it. Reading the fields the
/// cache needs (path, id, provider id, display name) from the registry has no native footprint, and likewise
/// replaces the per-file WinRT <c>StorageProvider.DisplayName</c> read.
/// </para>
/// <para>
/// Registry layout: each subkey of <c>SyncRootManager</c> is a sync-root id (<c>provider-id!security-id!account-id</c>);
/// the local path(s) live as values under that key's <c>UserSyncRoots</c> subkey, one value per user SID, and the
/// provider's friendly name lives in the sync-root key's <c>DisplayNameResource</c> value (often an <c>@dll,-id</c>
/// indirect string). The registry reads use the BCL <see cref="Registry"/> API (no COM/WinRT object;
/// <see cref="Microsoft.Win32.RegistryKey"/> owns and disposes the underlying native registry handle); the only
/// native call is <c>SHLoadIndirectString</c> to resolve the indirect display-name form.
/// </para>
/// </remarks>
internal sealed class SyncRootRegistryReader : ISyncRootRegistryReader
{
    private const string SyncRootManagerKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";
    private const string UserSyncRootsSubKeyName = "UserSyncRoots";
    private const string DisplayNameResourceValueName = "DisplayNameResource";

    public IReadOnlyList<SyncRootRegistration> ReadRegisteredSyncRoots()
    {
        try
        {
            using var managerKey = Registry.LocalMachine.OpenSubKey(SyncRootManagerKeyPath);
            if (managerKey is null)
            {
                return [];
            }

            var registrations = new List<SyncRootRegistration>();
            foreach (var syncRootId in managerKey.GetSubKeyNames())
            {
                AddRegistrationsForSyncRoot(managerKey, syncRootId, registrations);
            }

            return registrations;
        }
        catch (Exception exception) when (exception is IOException or System.Security.SecurityException or UnauthorizedAccessException)
        {
            // Best-effort, matching how the WinRT enumeration degraded: an inaccessible hive means "no sync roots".
            return [];
        }
    }

    /// <summary>
    /// Appends one <see cref="SyncRootRegistration"/> per path registered under a sync root's <c>UserSyncRoots</c>
    /// subkey. Skips silently when the sync-root key or its <c>UserSyncRoots</c> subkey is missing.
    /// </summary>
    private static void AddRegistrationsForSyncRoot(RegistryKey managerKey, string syncRootId, List<SyncRootRegistration> registrations)
    {
        using var syncRootKey = managerKey.OpenSubKey(syncRootId);
        using var userSyncRootsKey = syncRootKey?.OpenSubKey(UserSyncRootsSubKeyName);
        if (userSyncRootsKey is null)
        {
            return;
        }

        var providerId = ExtractProviderId(syncRootId);
        var displayName = ResolveDisplayName(syncRootKey?.GetValue(DisplayNameResourceValueName) as string);
        foreach (var userSid in userSyncRootsKey.GetValueNames())
        {
            if (userSyncRootsKey.GetValue(userSid) is string path && !string.IsNullOrWhiteSpace(path))
            {
                registrations.Add(new SyncRootRegistration(path, syncRootId, providerId, displayName));
            }
        }
    }

    /// <summary>
    /// Resolves a <c>DisplayNameResource</c> value to a friendly name. An <c>@</c>-prefixed value is a MUI indirect
    /// string (<c>@path,-id</c>) resolved via <c>SHLoadIndirectString</c>; any other value is already a literal name.
    /// Returns empty on absence or resolution failure — a missing provider name must not break the cloud read.
    /// </summary>
    private static string ResolveDisplayName(string? displayNameResource)
    {
        if (string.IsNullOrWhiteSpace(displayNameResource))
        {
            return string.Empty;
        }

        if (displayNameResource[0] != '@')
        {
            return displayNameResource;
        }

        Span<char> buffer = stackalloc char[512];
        if (PInvoke.SHLoadIndirectString(displayNameResource, buffer).Failed)
        {
            return string.Empty;
        }

        var terminator = buffer.IndexOf('\0');
        return new string(buffer[..(terminator < 0 ? buffer.Length : terminator)]);
    }

    /// <summary>
    /// Extracts the provider id — the leading segment of the sync-root id before the first <c>!</c>, per the
    /// documented <c>provider-id!security-id!account-id</c> format. Returns the whole id when no <c>!</c> is present.
    /// </summary>
    private static string ExtractProviderId(string syncRootId)
    {
        var separatorIndex = syncRootId.IndexOf('!');
        return separatorIndex >= 0 ? syncRootId[..separatorIndex] : syncRootId;
    }
}
