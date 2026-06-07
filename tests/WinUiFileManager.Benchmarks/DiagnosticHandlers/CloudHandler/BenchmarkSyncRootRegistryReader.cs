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
/// <c>InspectorCloudDiagnosticsHandler</c> short-circuits before its WinRT branch
/// (<c>StorageFile.GetFromPathAsync</c> → <c>Provider</c> → <c>Properties.RetrievePropertiesAsync</c>) — the exact
/// COM-allocating path whose native footprint the benchmark exists to measure. Forcing a match makes that branch run
/// for every request, which is the production scenario for a user whose files live under OneDrive/SharePoint.
/// </remarks>
internal sealed class BenchmarkSyncRootRegistryReader : ISyncRootRegistryReader
{
    private readonly IReadOnlyList<SyncRootRegistration> _registrations;

    public BenchmarkSyncRootRegistryReader(string syncRootPath)
    {
        _registrations =
        [
            new SyncRootRegistration(syncRootPath, "Benchmark!S-1-5-21!account", "Benchmark"),
        ];
    }

    public IReadOnlyList<SyncRootRegistration> ReadRegisteredSyncRoots() => _registrations;
}
