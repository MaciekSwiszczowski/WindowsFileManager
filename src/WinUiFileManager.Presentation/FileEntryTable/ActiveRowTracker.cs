namespace WinUiFileManager.Presentation.FileEntryTable;

/// <summary>
/// Process-wide tracker of the "active" (cursor) row per pane, used to drive the per-row active indicator from a
/// compiled <c>x:Bind</c> function instead of an imperative visual-tree walk.
/// </summary>
/// <remarks>
/// <para>
/// The previous approach walked each realised row container's visual subtree via <c>VisualTreeHelper.GetChild</c>
/// on every selection, which created a finalizer-tracked COM wrapper (<c>ReferenceTrackerNativeObjectWrapper</c>)
/// per visited node — a large native/finalizer churn while navigating with the arrow keys. Here the indicator's
/// opacity is bound to <see cref="IndicatorOpacity"/>; selecting a row only calls <see cref="SetActive"/>, which
/// bumps the observable <see cref="Version"/>. Every realised row's binding then re-evaluates (a cheap managed
/// reference comparison) and the matching row shows its indicator. No tree walk, no COM wrappers, and
/// virtualization realize is handled automatically because the binding re-runs when a container is reused.
/// </para>
/// <para>
/// The active row is tracked by its <see cref="FileSystemEntryModel"/> identity, keyed by pane. Model instances are
/// unique per pane (each pane scans independently), so comparing by reference still distinguishes the two panes'
/// indicators. The parent (<c>..</c>) row has no model, so selecting it simply shows no indicator.
/// </para>
/// <para>
/// <b>This is not a row cache.</b> It does not hold the table's rows or models — only the single active row's model
/// <i>per pane</i>, so the dictionary has at most one entry per pane (two total for a dual-pane window). Each
/// <see cref="SetActive"/> replaces that pane's one entry; nothing accumulates with row count or navigation history.
/// </para>
/// <para>
/// <b>Folder change.</b> There is no explicit clear on navigation. When a pane loads a new folder the new listing's
/// selection fires a selection-changed message, which calls <see cref="SetActive"/> and replaces the pane's entry
/// (or clears it via a null/parent row when the new folder has no selection). Until that next call the pane's entry
/// holds the previous folder's model — a single, bounded reference (one per pane), not a leak. It is also visually
/// harmless: the new folder's rows have fresh model instances, so <see cref="IsActive"/> reference-compares to false
/// and no indicator shows until the new selection lands. If a future change can leave a pane with rows but no
/// selection event, call <c>SetActive(paneIdentity, null)</c> on listing change to drop the stale reference eagerly.
/// </para>
/// </remarks>
public sealed partial class ActiveRowTracker : ObservableObject
{
    /// <summary>Shared instance bound from the row template and written by the pane behaviors.</summary>
    public static ActiveRowTracker Instance { get; } = new();

    private readonly Dictionary<string, FileSystemEntryModel> _activeModelsByPane = [];

    /// <summary>Monotonic change token; bound <c>OneWay</c> so row indicators re-evaluate when the active row changes.</summary>
    [ObservableProperty]
    public partial int Version { get; private set; }

    /// <summary>
    /// Sets (or clears) the active row for <paramref name="paneIdentity"/> and bumps <see cref="Version"/> when it
    /// actually changes. A null row (or a row with no model, e.g. the parent entry) clears the pane's active row.
    /// </summary>
    public void SetActive(string paneIdentity, FileListingRow? row)
    {
        var model = row?.Model;
        if (model is null)
        {
            if (_activeModelsByPane.Remove(paneIdentity))
            {
                Version++;
            }

            return;
        }

        if (_activeModelsByPane.TryGetValue(paneIdentity, out var current) && ReferenceEquals(current, model))
        {
            return;
        }

        _activeModelsByPane[paneIdentity] = model;
        Version++;
    }

    /// <summary>True when <paramref name="model"/> is the active row's model of any pane (reference identity).</summary>
    public bool IsActive(FileSystemEntryModel? model)
    {
        if (model is null)
        {
            return false;
        }

        foreach (var activeModel in _activeModelsByPane.Values)
        {
            if (ReferenceEquals(activeModel, model))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>x:Bind</c> function for a row indicator's opacity, bound to the row's <c>Model</c>. <paramref name="changeToken"/>
    /// is the observed <see cref="Version"/> path — unused in the body, it exists only to retrigger the binding when
    /// the active row changes.
    /// </summary>
    public static double IndicatorOpacity(FileSystemEntryModel? model, int changeToken) =>
        Instance.IsActive(model) ? 1d : 0d;
}
