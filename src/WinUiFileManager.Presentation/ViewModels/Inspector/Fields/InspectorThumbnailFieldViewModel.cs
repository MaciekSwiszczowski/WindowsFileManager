namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

public sealed partial class InspectorThumbnailFieldViewModel : InspectorFieldViewModel
{
    public InspectorThumbnailFieldViewModel(
        FileInspectorCategory category,
        string key,
        string tooltip,
        string value = "",
        int sortOrder = 0)
        : base(category, key, tooltip, value, sortOrder)
    {
    }

    public override InspectorFieldTemplate FieldTemplate => InspectorFieldTemplate.Thumbnail;

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
