namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorToggleFieldViewModel : InspectorFieldViewModelBase
{
    public delegate InspectorToggleFieldViewModel ToggleFactory(FileInspectorCategory category, string key, string tooltip, string value);

    public InspectorToggleFieldViewModel(
        FileInspectorCategory category,
        string key,
        string tooltip,
        string value = "")
        : base(category, key, tooltip, value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    public override InspectorFieldType FieldType => InspectorFieldType.Toggle;

    [ObservableProperty]
    public partial bool IsToggleOn { get; set; }

    public IAsyncRelayCommand? ToggleCommand { get; private set; }

    public bool CanInteract => !IsLoading && !IsUnavailable;

    public void ConfigureRefreshDrivenToggle(Func<bool, Task> toggleAsync)
    {
        ToggleCommand = new AsyncRelayCommand(() => SendRefreshDrivenToggleRequestAsync(toggleAsync));
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

    private async Task SendRefreshDrivenToggleRequestAsync(Func<bool, Task> toggleAsync)
    {
        await toggleAsync(!IsToggleOn);
    }
}
