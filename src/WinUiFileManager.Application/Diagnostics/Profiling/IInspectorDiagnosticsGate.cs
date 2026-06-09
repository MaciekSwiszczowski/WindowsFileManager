namespace WinUiFileManager.Application.Diagnostics.Profiling;

/// <summary>
/// Debug-only profiling gate: stores a per-<see cref="DiagnosticsCategory"/> <see cref="InspectorDiagnosticsMode"/>
/// that the inspector diagnostics handlers consult before running. Lets a developer disable a handler or run it
/// without publishing, to measure individual handler cost. Shared singleton; thread-safe.
/// </summary>
/// <remarks>
/// Writes go through <see cref="InspectorDiagnosticsProfiling.SetMode"/> (a <c>[Conditional("DEBUG")]</c> wrapper),
/// so in a Release build every write call site is elided by the compiler: the gate is never mutated and
/// <see cref="GetMode"/> always returns <see cref="InspectorDiagnosticsMode.Default"/>, leaving the handlers
/// behaving exactly as if the gate did not exist. No <c>#if</c> blocks are needed at call sites.
/// </remarks>
public interface IInspectorDiagnosticsGate
{
    /// <summary>Current mode for <paramref name="category"/>; <see cref="InspectorDiagnosticsMode.Default"/> when unset.</summary>
    public InspectorDiagnosticsMode GetMode(DiagnosticsCategory category);

    /// <summary>Sets the mode for <paramref name="category"/>. Prefer <see cref="InspectorDiagnosticsProfiling.SetMode"/>.</summary>
    public void SetMode(DiagnosticsCategory category, InspectorDiagnosticsMode mode);
}
