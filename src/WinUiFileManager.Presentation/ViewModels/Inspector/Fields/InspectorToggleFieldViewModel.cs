namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// An inspector field rendered as an interactive toggle for a writable boolean attribute (e.g. Read Only, Hidden,
/// Archive). The toggle's visual state is derived from the field value ("Yes"/"No"); flipping it dispatches an
/// attribute-change request and then waits for the resulting refresh to push the authoritative value back.
/// </summary>
/// <remarks>
/// "Refresh-driven": the toggle does not optimistically update <see cref="IsToggleOn"/>. Instead it disables itself
/// (<see cref="CanExecuteToggle"/> = false), requests the change via the supplied callback, and relies on the
/// subsequent diagnostics refresh updating <see cref="InspectorFieldViewModelBase.Value"/> (which re-derives
/// <see cref="IsToggleOn"/> in <see cref="OnFieldValueChanged"/>). <see cref="ResetToggleCommand"/> re-enables it.
/// </remarks>
public sealed partial class InspectorToggleFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorToggleFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
        IsToggleOn = string.Equals(request.Value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public override InspectorFieldType FieldType => InspectorFieldType.Toggle;

    /// <summary>Visual toggle state, derived from the field value ("Yes" = on).</summary>
    [ObservableProperty]
    public partial bool IsToggleOn { get; set; }

    /// <summary>Whether the toggle command can run; set false while a change is pending so the user can't double-fire.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool CanExecuteToggle { get; set; } = true;

    /// <summary>The command invoked when the user flips the toggle; assigned by <see cref="ConfigureRefreshDrivenToggle"/>.</summary>
    public IAsyncRelayCommand? ToggleCommand { get; private set; }

    /// <summary>Whether the toggle is interactable: not loading and not unavailable.</summary>
    public bool CanInteract => !IsLoading && !IsUnavailable;

    /// <summary>
    /// Wires the toggle to an attribute-change callback. <paramref name="toggleAsync"/> receives the desired new
    /// state (the inverse of the current one) and is responsible for dispatching the change request.
    /// </summary>
    public void ConfigureRefreshDrivenToggle(Func<bool, Task> toggleAsync)
    {
        ToggleCommand = new AsyncRelayCommand(() => SendRefreshDrivenToggleRequestAsync(toggleAsync), () => CanExecuteToggle);
        OnPropertyChanged(nameof(ToggleCommand));
    }

    /// <summary>Re-enables the toggle (called when the selection changes so a new item starts interactive).</summary>
    public void ResetToggleCommand()
    {
        CanExecuteToggle = true;
    }

    /// <inheritdoc/>
    protected override void OnFieldStateChanged()
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    /// <inheritdoc/>
    protected override void OnFieldValueChanged(string value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Disables further toggling and requests the inverse state; the value updates via the refresh that follows.</summary>
    private Task SendRefreshDrivenToggleRequestAsync(Func<bool, Task> toggleAsync)
    {
        CanExecuteToggle = false;
        return toggleAsync(!IsToggleOn);
    }
}
