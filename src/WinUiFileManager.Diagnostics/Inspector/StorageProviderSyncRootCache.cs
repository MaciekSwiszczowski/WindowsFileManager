using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Process-local cache of registered Windows cloud sync-root snapshots used by the cloud diagnostics handler.
/// </summary>
/// <remarks>
/// Looking up a sync root by repeatedly opening <c>StorageFolder</c> instances for a path and each ancestor is
/// expensive, especially in benchmarks that send many requests under the same few parent folders. This cache reads
/// the registered sync-root list once per short interval via <see cref="ISyncRootRegistryReader"/> and performs
/// ordinal-insensitive path-prefix matching in managed code. The reader returns plain registration records read
/// from the registry, not WinRT <c>StorageProviderSyncRootInfo</c> objects, so neither the reader nor this cache
/// retains native provider-registration state — that WinRT enumeration was the earlier native-memory leak source.
/// The short expiry keeps the main application responsive to provider registration changes without reintroducing
/// per-file WinRT ancestor probes.
/// </remarks>
public sealed class StorageProviderSyncRootCache
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private readonly ISyncRootRegistryReader _syncRootRegistryReader;
    private readonly Lock _syncRootGate = new();
    private StorageProviderSyncRootSnapshot[] _syncRoots = [];
    private DateTimeOffset _syncRootsLoadedAt = DateTimeOffset.MinValue;

    /// <param name="syncRootRegistryReader">
    /// Source of registered sync roots; injected so the path-matching logic is unit-testable against a fake reader.
    /// </param>
    public StorageProviderSyncRootCache(ISyncRootRegistryReader syncRootRegistryReader)
    {
        _syncRootRegistryReader = syncRootRegistryReader;
    }

    /// <summary>
    /// Finds the deepest registered sync root that contains <paramref name="path"/>, or null when no root matches.
    /// </summary>
    /// <param name="path">File or directory path to classify.</param>
    /// <returns>The nearest registered sync-root snapshot for <paramref name="path"/>, or null.</returns>
    internal StorageProviderSyncRootSnapshot? FindForPath(string path)
    {
        if (!TryNormalizePath(path, out var normalizedPath))
        {
            return null;
        }

        StorageProviderSyncRootSnapshot? bestMatch = null;
        var bestMatchLength = -1;

        foreach (var syncRoot in GetSyncRoots())
        {
            var rootPath = syncRoot.Path;
            if (string.IsNullOrWhiteSpace(rootPath)
                || !TryNormalizePath(rootPath, out var normalizedRootPath)
                || !IsSameOrChildPath(normalizedPath, normalizedRootPath)
                || normalizedRootPath.Length <= bestMatchLength)
            {
                continue;
            }

            bestMatch = syncRoot;
            bestMatchLength = normalizedRootPath.Length;
        }

        return bestMatch;
    }

    private StorageProviderSyncRootSnapshot[] GetSyncRoots()
    {
        lock (_syncRootGate)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - _syncRootsLoadedAt <= CacheLifetime)
            {
                return _syncRoots;
            }

            _syncRoots = LoadSyncRoots();
            _syncRootsLoadedAt = now;
            return _syncRoots;
        }
    }

    /// <summary>
    /// Maps the reader's registration records into cache snapshots. The reader is best-effort and never throws,
    /// so no defensive catch is needed here; an empty result simply means "no cloud sync roots are registered".
    /// </summary>
    private StorageProviderSyncRootSnapshot[] LoadSyncRoots()
    {
        var registrations = _syncRootRegistryReader.ReadRegisteredSyncRoots();
        if (registrations.Count == 0)
        {
            return [];
        }

        var snapshots = new StorageProviderSyncRootSnapshot[registrations.Count];
        for (var index = 0; index < registrations.Count; index++)
        {
            var registration = registrations[index];
            snapshots[index] = new StorageProviderSyncRootSnapshot(
                registration.Path,
                registration.Id,
                registration.ProviderId,
                registration.DisplayName);
        }

        return snapshots;
    }

    private static bool TryNormalizePath(string path, out string normalizedPath)
    {
        try
        {
            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch
        {
            normalizedPath = string.Empty;
            return false;
        }
    }

    private static bool IsSameOrChildPath(string path, string rootPath)
    {
        if (string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (EndsWithDirectorySeparator(rootPath))
        {
            return true;
        }

        return path.Length > rootPath.Length && IsDirectorySeparator(path[rootPath.Length]);
    }

    private static bool EndsWithDirectorySeparator(string path) =>
        path.Length > 0 && IsDirectorySeparator(path[^1]);

    private static bool IsDirectorySeparator(char character) =>
        character is '\\' or '/';
}
