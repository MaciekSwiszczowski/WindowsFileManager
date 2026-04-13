using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinUiFileManager.Domain.ValueObjects;

namespace WinUiFileManager.Presentation.ViewModels;

public sealed partial class FavouritesDialogViewModel : ObservableObject
{
    public FavouritesDialogViewModel(IReadOnlyList<FavouriteFolder> favourites)
    {
        Favourites = new ObservableCollection<FavouriteFolder>(favourites);
    }

    public ObservableCollection<FavouriteFolder> Favourites { get; }

    public event Action<FavouriteFolderId>? FavouriteRemoved;

    public event Action<FavouriteFolderId>? FavouriteOpened;

    [RelayCommand]
    private void RemoveFavourite(FavouriteFolderId id)
    {
        var item = Favourites.FirstOrDefault(f => f.Id == id);
        if (item is not null)
            Favourites.Remove(item);

        FavouriteRemoved?.Invoke(id);
    }

    [RelayCommand]
    private void OpenFavourite(FavouriteFolderId id)
    {
        FavouriteOpened?.Invoke(id);
    }
}
