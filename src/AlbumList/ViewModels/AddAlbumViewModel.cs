using AlbumList.Data;
using AlbumList.Models;
using AlbumList.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlbumList.ViewModels;

public partial class AddAlbumViewModel : ObservableObject
{
    private readonly AlbumDatabase _database;
    private readonly MetadataBackgroundWorker _worker;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    public AddAlbumViewModel(AlbumDatabase database, MetadataBackgroundWorker worker)
    {
        _database = database;
        _worker = worker;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Artist))
            return;

        var duplicate = await _database.FindDuplicateAsync(Name, Artist);
        if (duplicate is not null)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                "Duplicate", "Album already in your list", "OK");
            await Shell.Current.GoToAsync($"entry?Id={duplicate.Id}");
            return;
        }

        var album = new Album { Name = Name, Artist = Artist };
        await _database.UpsertAsync(album);
        _worker.Enqueue(album.Id);

        Name = string.Empty;
        Artist = string.Empty;

        await Shell.Current.GoToAsync("..");
    }
}
