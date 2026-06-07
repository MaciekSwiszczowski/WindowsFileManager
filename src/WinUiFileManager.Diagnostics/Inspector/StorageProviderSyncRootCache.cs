using Windows.Storage.Provider;

namespace WinUiFileManager.Diagnostics.Inspector;

/// <summary>
/// Process-local cache of registered Windows cloud sync-root snapshots used by the cloud diagnostics handler.
/// </summary>
/// <remarks>
/// Looking up a sync root by repeatedly opening <c>StorageFolder</c> instances for a path and each ancestor is
/// expensive, especially in benchmarks that send many requests under the same few parent folders. This cache reads
/// the registered sync-root list once per short interval and performs ordinal-insensitive path-prefix matching in
/// managed code. The cache stores only managed strings, not <see cref="StorageProviderSyncRootInfo"/> WinRT
/// objects, so refreshes do not retain native provider-registration objects for the container lifetime. The
/// short expiry keeps the main application responsive to provider registration changes without reintroducing
/// per-file WinRT ancestor probes.
/// </remarks>
public sealed class StorageProviderSyncRootCache
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private readonly Lock _syncRootGate = new();
    private StorageProviderSyncRootSnapshot[] _syncRoots = [];
    private DateTimeOffset _syncRootsLoadedAt = DateTimeOffset.MinValue;

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

    private static StorageProviderSyncRootSnapshot[] LoadSyncRoots()
    {
        try
        {
            var snapshots = new List<StorageProviderSyncRootSnapshot>();
            foreach (var syncRoot in StorageProviderSyncRootManager.GetCurrentSyncRoots())
            {
                if (TryCreateSnapshot(syncRoot) is { } snapshot)
                {
                    snapshots.Add(snapshot);
                }
            }

            return [.. snapshots];
        }
        catch
        {
            return [];
        }
    }

    private static StorageProviderSyncRootSnapshot? TryCreateSnapshot(StorageProviderSyncRootInfo syncRoot)
    {
        try
        {
            var path = syncRoot.Path?.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return new StorageProviderSyncRootSnapshot(
                path,
                syncRoot.Id ?? string.Empty,
                syncRoot.ProviderId.ToString());
        }
        catch
        {
            return null;
        }
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
