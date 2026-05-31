namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Internal initialization contract for deferred field loaders. Separated from <see cref="IInspectorDeferredFieldLoader"/>
/// so the public load/cancel surface stays free of the wiring step. <see cref="InspectorViewModel"/> requires every
/// loader to implement this and calls <see cref="Initialize"/> with the shared <see cref="InspectorFieldValueUpdater"/>
/// before any load.
/// </summary>
internal interface IInspectorDeferredFieldLoaderInitializer
{
    /// <summary>Supplies the field-value updater the loader writes results into. Must be called exactly once before loading.</summary>
    public void Initialize(InspectorFieldValueUpdater fieldValueUpdater);
}
