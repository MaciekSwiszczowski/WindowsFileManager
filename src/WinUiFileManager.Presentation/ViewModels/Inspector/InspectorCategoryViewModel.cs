using CommunityToolkit.Mvvm.Input;
using WinUiFileManager.Application.Diagnostics.Profiling;
using WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

namespace WinUiFileManager.Presentation.ViewModels.Inspector;

/// <summary>
/// View model for one collapsible category section in the inspector (e.g. Basic, NTFS, Security). Holds the
/// section's fields and tracks its expand/collapse state and whether it currently has any visible fields
/// (used to hide empty sections during search filtering).
/// </summary>
/// <remarks>
/// In Debug builds, categories backed by a deferred diagnostics handler expose a three-state profiling toggle
/// (<see cref="CycleProfilingModeCommand"/>) that writes the shared <see cref="IInspectorDiagnosticsGate"/> to make
/// the handler run-without-responding or go inactive — a developer aid for measuring per-handler cost. The write
/// path is compiled out of Release (<see cref="InspectorDiagnosticsProfiling.SetMode"/>), and the control is hidden
/// unless <see cref="ProfilingControlAvailable"/>.
/// </remarks>
public sealed partial class InspectorCategoryViewModel : ObservableObject
{
    private readonly IInspectorDiagnosticsGate _diagnosticsGate;
    private readonly DiagnosticsCategory? _diagnosticsCategory;

    public InspectorCategoryViewModel(FileInspectorCategory category, IInspectorDiagnosticsGate diagnosticsGate)
    {
        Category = category;
        Name = category.GetDisplayName();
        _diagnosticsGate = diagnosticsGate;
        _diagnosticsCategory = category.ToDiagnosticsCategory();
    }

    /// <summary>The category this section represents.</summary>
    public FileInspectorCategory Category { get; }

    /// <summary>The category's display header.</summary>
    public string Name { get; }

    /// <summary>Whether the section is expanded in the UI.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    /// <summary>Whether any field in this section is currently visible; recomputed by <see cref="RefreshVisibility"/>.</summary>
    [ObservableProperty]
    public partial bool HasVisibleFields { get; set; }

    /// <summary>The fields shown in this section.</summary>
    public ObservableCollection<InspectorFieldViewModelBase> Fields { get; } = [];

    /// <summary>
    /// Current profiling mode for this category's handler. Drives <see cref="ProfilingModeLabel"/>. Only meaningful
    /// when <see cref="ProfilingControlAvailable"/> is true.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfilingModeLabel))]
    public partial InspectorDiagnosticsMode ProfilingMode { get; set; }

    /// <summary>True when the Debug-only profiling toggle should be shown (Debug build and a handler backs this category).</summary>
    public bool ProfilingControlAvailable => BuildEnvironment.IsDebug && _diagnosticsCategory is not null;

    /// <summary>Short button label reflecting the current profiling mode.</summary>
    public string ProfilingModeLabel => ProfilingMode switch
    {
        InspectorDiagnosticsMode.RunWithoutResponding => "Run, no refresh",
        InspectorDiagnosticsMode.Inactive => "Inactive",
        _ => "Active",
    };

    /// <summary>Recomputes <see cref="HasVisibleFields"/> from the current field visibility (e.g. after a search filter).</summary>
    public void RefreshVisibility()
    {
        HasVisibleFields = Fields.Any(static field => field.IsVisible);
    }

    /// <summary>Cycles Active → Run-without-responding → Inactive → Active and writes the shared gate (Debug-only).</summary>
    [RelayCommand]
    private void CycleProfilingMode()
    {
        if (_diagnosticsCategory is not { } diagnosticsCategory)
        {
            return;
        }

        ProfilingMode = ProfilingMode switch
        {
            InspectorDiagnosticsMode.Default => InspectorDiagnosticsMode.RunWithoutResponding,
            InspectorDiagnosticsMode.RunWithoutResponding => InspectorDiagnosticsMode.Inactive,
            _ => InspectorDiagnosticsMode.Default,
        };

        InspectorDiagnosticsProfiling.SetMode(_diagnosticsGate, diagnosticsCategory, ProfilingMode);
    }
}
