namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// Base view model for a single inspector field. Holds the field's identity (category/key/tooltip), its current
/// value, and shared UI state (visibility for search filtering, loading state, unavailable state). Subclasses add
/// behavior for richer field types (<see cref="InspectorToggleFieldViewModel"/>, <see cref="InspectorThumbnailFieldViewModel"/>).
/// </summary>
/// <remarks>
/// Provides two protected extension points so derived types can react without overriding the generated change
/// hooks: <see cref="OnFieldStateChanged"/> (loading changed) and <see cref="OnFieldValueChanged"/> (value changed).
/// </remarks>
public abstract partial class InspectorFieldViewModelBase : ObservableObject
{
    protected InspectorFieldViewModelBase(FileInspectorCategory category, string key, string tooltip, string value = "")
    {
        Category = category;
        Key = key;
        Tooltip = tooltip;
        Value = value;
    }

    /// <summary>The field type, used by the view to select a cell template. Defaults to <see cref="InspectorFieldType.Text"/>.</summary>
    public virtual InspectorFieldType FieldType => InspectorFieldType.Text;

    /// <summary>The category this field belongs to.</summary>
    public FileInspectorCategory Category { get; }

    /// <summary>Stable field key/label; used to address the field by name and shown as the row label.</summary>
    public string Key { get; }

    /// <summary>Tooltip/help text for the field.</summary>
    public string Tooltip { get; }

    /// <summary>The field's current raw value. Changing it raises <see cref="DisplayValue"/>/<see cref="IsUnavailable"/> and calls <see cref="OnFieldValueChanged"/>.</summary>
    [ObservableProperty]
    public partial string Value { get; set; }

    /// <summary>Whether the field is currently shown (toggled by search filtering).</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Whether a deferred load for this field is in progress; changing it calls <see cref="OnFieldStateChanged"/>.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>The value as displayed; a hook for subclasses/converters (currently the raw <see cref="Value"/>).</summary>
    public string DisplayValue => Value;

    /// <summary>
    /// Full value for a hover tooltip, or <c>null</c> (no tooltip) when empty. Lets a long value stay readable when
    /// the inspector is shrunk, without a blank tooltip on empty fields. Typed <c>object?</c> for <c>ToolTipService.ToolTip</c>.
    /// </summary>
    public object? ValueTooltip => string.IsNullOrEmpty(Value) ? null : Value;

    /// <summary>Concatenated category/key/value text the search filter matches against.</summary>
    public string SearchText => string.Concat(Category.GetDisplayName(), " ", Key, " ", DisplayValue);

    /// <summary>True when not loading and there is no value, i.e. the field has nothing to show (drives "unavailable" styling).</summary>
    public virtual bool IsUnavailable => !IsLoading && string.IsNullOrWhiteSpace(Value);

    /// <summary>Generated hook: when loading state flips, re-raise derived state and notify subclasses.</summary>
    partial void OnIsLoadingChanged(bool value)
    {
        NotifyValueStateChanged();
        OnFieldStateChanged();
    }

    /// <summary>Generated hook: when the value changes, re-raise derived state and notify subclasses.</summary>
    partial void OnValueChanged(string value)
    {
        NotifyValueStateChanged();
        OnFieldValueChanged(value);
    }

    /// <summary>Override to react to loading-state changes (e.g. recompute interactivity). Base is a no-op.</summary>
    protected virtual void OnFieldStateChanged()
    {
    }

    /// <summary>Override to react to value changes (e.g. derive toggle state from the text). Base is a no-op.</summary>
    protected virtual void OnFieldValueChanged(string value)
    {
    }

    /// <summary>Raises change notifications for the value-derived read-only properties (<see cref="DisplayValue"/>, <see cref="ValueTooltip"/>, <see cref="IsUnavailable"/>).</summary>
    protected void NotifyValueStateChanged()
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(ValueTooltip));
        OnPropertyChanged(nameof(IsUnavailable));
    }
}
