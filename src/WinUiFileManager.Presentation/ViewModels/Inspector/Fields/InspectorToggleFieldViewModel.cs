namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorToggleFieldViewModel : InspectorFieldViewModel
{
    public InspectorToggleFieldViewModel(
        FileInspectorCategory category,
        string key,
        string tooltip,
        string value = "")
        : base(category, key, tooltip, value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    public override InspectorFieldTemplate FieldTemplate => InspectorFieldTemplate.Toggle;

    [ObservableProperty]
    public partial bool IsToggleOn { get; set; }

    public IAsyncRelayCommand? ToggleCommand { get; private set; }

    public bool CanInteract => !IsLoading && !IsUnavailable;

    public void ConfigureToggle(Func<bool, Task<bool>> toggleAsync)
    {
        ToggleCommand = new AsyncRelayCommand(() => ToggleAsync(toggleAsync));
        OnPropertyChanged(nameof(ToggleCommand));
    }

    protected override void OnFieldStateChanged()
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    protected override void OnFieldValueChanged(string value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(CanInteract));
    }

    private async Task ToggleAsync(Func<bool, Task<bool>> toggleAsync)
    {
        var nextValue = !IsToggleOn;
        var previousValue = Value;
        var previousToggle = IsToggleOn;

        IsToggleOn = nextValue;
        Value = nextValue ? "Yes" : "No";

        if (await toggleAsync(nextValue))
        {
            return;
        }

        IsToggleOn = previousToggle;
        Value = previousValue;
    }
}
