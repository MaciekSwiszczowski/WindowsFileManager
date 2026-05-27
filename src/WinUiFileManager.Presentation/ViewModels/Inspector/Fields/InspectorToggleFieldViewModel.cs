namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorToggleFieldViewModel : InspectorFieldViewModelBase
{
    public delegate InspectorToggleFieldViewModel ToggleFactory(FileInspectorCategory category, string key, string tooltip, string value);

    public InspectorToggleFieldViewModel(FileInspectorCategory category, string key, string tooltip, string value = "")
        : base(category, key, tooltip, value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    public override InspectorFieldType FieldType => InspectorFieldType.Toggle;

    [ObservableProperty]
    public partial bool IsToggleOn { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
    public partial bool CanExecuteToggle { get; set; } = true;

    public IAsyncRelayCommand? ToggleCommand { get; private set; }

    public bool CanInteract => !IsLoading && !IsUnavailable;

    public void ConfigureRefreshDrivenToggle(Func<bool, Task> toggleAsync)
    {
        ToggleCommand = new AsyncRelayCommand(() => SendRefreshDrivenToggleRequestAsync(toggleAsync), () => CanExecuteToggle);
        OnPropertyChanged(nameof(ToggleCommand));
    }

    public void ResetToggleCommand()
    {
        CanExecuteToggle = true;
    }

    protected override void OnFieldStateChanged()
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    protected override void OnFieldValueChanged(string value)
    {
        IsToggleOn = string.Equals(value, "Yes", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendRefreshDrivenToggleRequestAsync(Func<bool, Task> toggleAsync)
    {
        CanExecuteToggle = false;
        await toggleAsync(!IsToggleOn);
    }
}
