using WinUiFileManager.Application.Diagnostics.Profiling;

namespace WinUiFileManager.Application.Tests.Fixtures;

public sealed class FakeInspectorDiagnosticsGate : IInspectorDiagnosticsGate
{
    public static FakeInspectorDiagnosticsGate Instance { get; } = new();

    public InspectorDiagnosticsMode GetMode(DiagnosticsCategory category) => InspectorDiagnosticsMode.Default;

    public void SetMode(DiagnosticsCategory category, InspectorDiagnosticsMode mode)
    {
    }
}
