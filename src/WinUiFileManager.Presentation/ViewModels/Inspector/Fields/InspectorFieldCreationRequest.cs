namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Immutable parameter object passed to the inspector field factories. Bundles the data needed to construct a
/// field view model so the factory delegates have a single argument.
/// </summary>
/// <param name="Category">The category the field belongs to.</param>
/// <param name="Key">The field's stable key/label; also the address used by the value updater and loaders.</param>
/// <param name="Tooltip">Tooltip/help text shown for the field.</param>
/// <param name="Value">Initial field value (usually empty).</param>
public sealed record InspectorFieldCreationRequest(
    FileInspectorCategory Category,
    string Key,
    string Tooltip,
    string Value);
