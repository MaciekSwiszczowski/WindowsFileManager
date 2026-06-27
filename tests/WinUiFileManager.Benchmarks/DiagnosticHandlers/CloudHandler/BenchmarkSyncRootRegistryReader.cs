using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;

namespace WinUiFileManager.Benchmarks.DiagnosticHandlers.CloudHandler;

/// <summary>
/// Test-only <see cref="ISyncRootRegistryReader"/> that reports a single, caller-supplied directory as a registered
/// cloud sync root. Registered over the real reader in <see cref="CloudHandlerBenchmarks"/> so that
/// <c>StorageProviderSyncRootCache.FindForPath</c> matches the benchmark's temp files.
/// </summary>
/// <remarks>
/// Without this, the real registry has no sync root covering the benchmark's temp directory, so
/// <c>InspectorCloudDiagnosticsHandler</c> short-circuits before it can report sync-root identity or the
/// attribute/CldApi placeholder state. Forcing a match models the production scenario for a user whose files live
/// under OneDrive/SharePoint while preserving the plain-local-file fast path.
/// </remarks>
internal sealed class BenchmarkSyncRootRegistryReader : ISyncRootRegistryReader
{
    private readonly IReadOnlyList<SyncRootRegistration> _registrations;

    public BenchmarkSyncRootRegistryReader(string syncRootPath)
    {
        _registrations =
        [
            new SyncRootRegistration(syncRootPath, "Benchmark!S-1-5-21!account", "Benchmark", "Benchmark"),
        ];
    }

    public IReadOnlyList<SyncRootRegistration> ReadRegisteredSyncRoots() => _registrations;
}
