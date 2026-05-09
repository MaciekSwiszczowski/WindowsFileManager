namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class CommandButtonsViewModel : ObservableObject, IDisposable
{
    private readonly IMessenger _messenger;
    private bool _disposed;

    public CommandButtonsViewModel(IMessenger? messenger = null)
    {
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        _messenger.Register<ToggleInspectorKeyPressedMessage>(this, OnToggleInspectorKeyPressed);
        _messenger.Register<ToggleInspectorRequestedMessage>(this, OnToggleInspectorRequested);
    }

    public Action? ToggleThemeAction { get; set; }

    [ObservableProperty]
    public partial bool IsInspectorVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool ParallelExecutionEnabled { get; set; }

    public ObservableCollection<FavouriteFolder> Favourites { get; } = [];

    public void SetFavourites(IEnumerable<FavouriteFolder> favourites)
    {
        Favourites.Clear();
        foreach (var favourite in favourites)
        {
            Favourites.Add(favourite);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _messenger.UnregisterAll(this);
    }

    private void OnToggleInspectorKeyPressed(object recipient, ToggleInspectorKeyPressedMessage _)
    {
        IsInspectorVisible = !IsInspectorVisible;
    }

    private void OnToggleInspectorRequested(object recipient, ToggleInspectorRequestedMessage message)
    {
        IsInspectorVisible = message.IsVisible;
    }

    partial void OnIsInspectorVisibleChanged(bool value)
    {
        _messenger.Send(new ToggleInspectorRequestedMessage(value));
    }

    [RelayCommand]
    private void Copy() => _messenger.Send(new CopyKeyPressedMessage());

    [RelayCommand]
    private void Move() => _messenger.Send(new MoveKeyPressedMessage());

    [RelayCommand]
    private void Rename() => _messenger.Send(new RenameKeyPressedMessage());

    [RelayCommand]
    private void Delete() => _messenger.Send(new DeleteKeyPressedMessage());

    [RelayCommand]
    private void CreateFolder() => _messenger.Send(new CreateFolderKeyPressedMessage());

    [RelayCommand]
    private void CopyPath()
    {
        _messenger.Send(new CopyPathKeyPressedMessage());
    }

    [RelayCommand]
    private void ToggleTheme() => ToggleThemeAction?.Invoke();
}
