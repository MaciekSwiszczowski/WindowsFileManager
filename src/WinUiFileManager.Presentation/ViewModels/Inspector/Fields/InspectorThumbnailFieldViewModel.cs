namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorThumbnailFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorThumbnailFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
    }

    public override InspectorFieldType FieldType => InspectorFieldType.Thumbnail;

    [ObservableProperty]
    public partial ImageSource? ThumbnailSource { get; set; }

    public bool ShowsThumbnail => !IsLoading && ThumbnailSource is not null;

    public override bool IsUnavailable => !IsLoading && ThumbnailSource is null;

    partial void OnThumbnailSourceChanged(ImageSource? value)
    {
        NotifyValueStateChanged();
        OnPropertyChanged(nameof(ShowsThumbnail));
    }

    protected override void OnFieldStateChanged()
    {
        OnPropertyChanged(nameof(ShowsThumbnail));
    }
}
