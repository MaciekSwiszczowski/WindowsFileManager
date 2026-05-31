namespace WinUiFileManager.Presentation.ViewModels.Inspector.Fields;

/// <summary>
/// An inspector field that renders an image preview. Availability and "shows thumbnail" state are derived from
/// <see cref="ThumbnailSource"/> rather than the text value, so this overrides <see cref="IsUnavailable"/>.
/// </summary>
public sealed partial class InspectorThumbnailFieldViewModel : InspectorFieldViewModelBase
{
    public InspectorThumbnailFieldViewModel(InspectorFieldCreationRequest request)
        : base(request.Category, request.Key, request.Tooltip, request.Value)
    {
    }

    /// <inheritdoc/>
    public override InspectorFieldType FieldType => InspectorFieldType.Thumbnail;

    /// <summary>The decoded thumbnail image, or <c>null</c> when none is available. Must be set on the UI thread (WinUI image type).</summary>
    [ObservableProperty]
    public partial ImageSource? ThumbnailSource { get; set; }

    /// <summary>True when a thumbnail is loaded and not currently (re)loading.</summary>
    public bool ShowsThumbnail => !IsLoading && ThumbnailSource is not null;

    /// <inheritdoc/>
    public override bool IsUnavailable => !IsLoading && ThumbnailSource is null;

    /// <summary>Generated hook: when the image changes, re-raise the value-derived and <see cref="ShowsThumbnail"/> state.</summary>
    partial void OnThumbnailSourceChanged(ImageSource? value)
    {
        NotifyValueStateChanged();
        OnPropertyChanged(nameof(ShowsThumbnail));
    }

    /// <inheritdoc/>
    protected override void OnFieldStateChanged()
    {
        OnPropertyChanged(nameof(ShowsThumbnail));
    }
}
