using System.Collections.ObjectModel;
using AlbumList.Data;
using AlbumList.Messages;
using AlbumList.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace AlbumList.ViewModels;

public partial class AlbumListViewModel : ObservableObject
{
    private readonly AlbumDatabase _database;

    [ObservableProperty]
    private ObservableCollection<Album> _albums = new();

    [ObservableProperty]
    private AlbumSort _selectedSort = AlbumSort.Name;

    public IReadOnlyList<AlbumSort> SortOptions { get; } = Enum.GetValues<AlbumSort>();

    public AlbumListViewModel(AlbumDatabase database)
    {
        _database = database;
        WeakReferenceMessenger.Default.Register<AlbumsUpdatedMessage>(this, (r, m) =>
            MainThread.BeginInvokeOnMainThread(async () => await LoadAsync()));
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var list = await _database.GetAllAsync(SelectedSort);
        Albums = new ObservableCollection<Album>(list);
    }

    [RelayCommand]
    private async Task SortAsync(AlbumSort sort)
    {
        SelectedSort = sort;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync(Album album)
    {
        await _database.DeleteAsync(album.Id);
        Albums.Remove(album);
    }

    [RelayCommand]
    private async Task ViewAsync(Album album)
    {
        await Shell.Current.GoToAsync($"entry?Id={album.Id}");
    }
}
