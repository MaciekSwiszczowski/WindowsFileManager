using System.Diagnostics;

namespace WinUiFileManager.Application.Diagnostics.Profiling;

/// <summary>
/// Debug-only entry point for writing the inspector diagnostics profiling gate. The single mutating method is
/// marked <see cref="ConditionalAttribute"/> <c>"DEBUG"</c>, so every call to it is removed by the compiler in a
/// Release build — the gate then stays unwritten and all categories report <see cref="InspectorDiagnosticsMode.Default"/>.
/// </summary>
public static class InspectorDiagnosticsProfiling
{
    /// <summary>
    /// Sets the profiling mode for <paramref name="category"/>. Debug-only: the call is compiled out of Release.
    /// </summary>
    [Conditional("DEBUG")]
    public static void SetMode(IInspectorDiagnosticsGate gate, DiagnosticsCategory category, InspectorDiagnosticsMode mode)
    {
        ArgumentNullException.ThrowIfNull(gate);
        gate.SetMode(category, mode);
    }
}
