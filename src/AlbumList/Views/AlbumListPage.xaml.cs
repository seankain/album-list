using AlbumList.Models;
using AlbumList.ViewModels;

namespace AlbumList.Views;

public partial class AlbumListPage : ContentPage
{
    private Album? _menuAlbum;

    public AlbumListViewModel ViewModel => (AlbumListViewModel)BindingContext;

    public AlbumListPage(AlbumListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnMoreButtonClicked(object sender, EventArgs e)
    {
        if (sender is ImageButton button)
        {
            _menuAlbum = button.BindingContext as Album;
            FlyoutBase.ShowAttachedFlyout(button);
        }
    }

    private void OnViewMenuItemClicked(object sender, EventArgs e)
    {
        if (_menuAlbum is not null)
            ViewModel.ViewCommand.Execute(_menuAlbum);
    }

    private void OnDeleteMenuItemClicked(object sender, EventArgs e)
    {
        if (_menuAlbum is not null)
            ViewModel.DeleteCommand.Execute(_menuAlbum);
    }
}
