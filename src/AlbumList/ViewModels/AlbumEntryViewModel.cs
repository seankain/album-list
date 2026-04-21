using AlbumList.Data;
using AlbumList.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlbumList.ViewModels;

[QueryProperty("Id", "Id")]
public partial class AlbumEntryViewModel : ObservableObject
{
    private readonly AlbumDatabase _database;
    private Album? _album;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _artist = string.Empty;

    [ObservableProperty]
    private DateTime? _releaseDate;

    [ObservableProperty]
    private int _personalRating;

    [ObservableProperty]
    private int _criticalRating;

    [ObservableProperty]
    private string? _summary;

    public AlbumEntryViewModel(AlbumDatabase database)
    {
        _database = database;
    }

    partial void OnIdChanged(int value) => _ = LoadAlbumAsync(value);

    private async Task LoadAlbumAsync(int id)
    {
        _album = await _database.GetAsync(id);
        if (_album is null) return;

        Name = _album.Name;
        Artist = _album.Artist;
        ReleaseDate = _album.ReleaseDate;
        PersonalRating = _album.PersonalRating;
        CriticalRating = _album.CriticalRating;
        Summary = _album.Summary;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_album is null) return;

        _album.PersonalRating = PersonalRating;
        _album.CriticalRating = CriticalRating;
        _album.Summary = Summary;

        await _database.UpsertAsync(_album);
        await Shell.Current.GoToAsync("..");
    }
}
