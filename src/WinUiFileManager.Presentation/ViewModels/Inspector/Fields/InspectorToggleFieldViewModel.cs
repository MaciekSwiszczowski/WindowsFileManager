namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorToggleFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorToggleFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
        IsToggleOn = string.Equals(request.Value, "Yes", StringComparison.OrdinalIgnoreCase);
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
