using System.Collections.Concurrent;
using WinUiFileManager.Application.Diagnostics.Profiling;

namespace WinUiFileManager.Infrastructure.Diagnostics;

/// <summary>
/// In-memory <see cref="IInspectorDiagnosticsGate"/>: a thread-safe per-category mode store backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Singleton; written by the (Debug-only) inspector profiling UI
/// and read by the diagnostics handlers.
/// </summary>
internal sealed class InspectorDiagnosticsGate : IInspectorDiagnosticsGate
{
    private readonly ConcurrentDictionary<DiagnosticsCategory, InspectorDiagnosticsMode> _modes = new();

    public InspectorDiagnosticsMode GetMode(DiagnosticsCategory category) =>
        _modes.GetValueOrDefault(category, InspectorDiagnosticsMode.Default);

    public void SetMode(DiagnosticsCategory category, InspectorDiagnosticsMode mode) =>
        _modes[category] = mode;
}
