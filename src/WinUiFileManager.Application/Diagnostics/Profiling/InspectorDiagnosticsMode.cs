namespace WinUiFileManager.Application.Diagnostics.Profiling;

/// <summary>
/// Per-category profiling mode for a deferred inspector diagnostics handler. A Debug-only diagnostic aid for
/// isolating the cost of individual handlers; in Release the gate that stores it is never written, so every
/// category stays <see cref="Default"/>.
/// </summary>
public enum InspectorDiagnosticsMode
{
    /// <summary>Normal operation: the handler runs and publishes its response to the inspector.</summary>
    Default,

    /// <summary>
    /// The handler still executes its full load (so its cost is measured), but its response is discarded instead of
    /// published — the inspector category is not refreshed. Isolates handler compute from messaging/UI cost.
    /// </summary>
    RunWithoutResponding,

    /// <summary>The handler does not execute at all — zero cost. The inspector category is not refreshed.</summary>
    Inactive,
}
