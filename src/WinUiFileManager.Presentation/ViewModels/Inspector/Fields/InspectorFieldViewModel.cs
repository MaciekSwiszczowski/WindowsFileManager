namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public partial class InspectorFieldViewModel : ObservableObject
{

    public InspectorFieldViewModel(FileInspectorCategory category, string key, string tooltip, string value = "", int sortOrder = 0)
    {
        Category = category;
        Key = key;
        Tooltip = tooltip;
        Value = value;
        SortOrder = sortOrder;
        SearchText = $"{Category.GetDisplayName()} {Key}";
    }

    public virtual InspectorFieldTemplate FieldTemplate => InspectorFieldTemplate.Text;

    public FileInspectorCategory Category { get; }

    public string Key { get; }

    public string Tooltip { get; }

    public int SortOrder { get; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public string DisplayValue => IsUnavailable ? "Not available" : Value;

    public virtual bool IsUnavailable => !IsLoading && string.IsNullOrWhiteSpace(Value);

    public string SearchText => string.Concat(field, Value);

    partial void OnIsLoadingChanged(bool value)
    {
        NotifyValueStateChanged();
        OnFieldStateChanged();
    }

    partial void OnValueChanged(string value)
    {
        NotifyValueStateChanged();
        OnFieldValueChanged(value);
    }

    protected virtual void OnFieldStateChanged()
    {
    }

    protected virtual void OnFieldValueChanged(string value)
    {
    }

    protected void NotifyValueStateChanged()
    {
        OnPropertyChanged(nameof(DisplayValue));
        OnPropertyChanged(nameof(IsUnavailable));
    }
}
