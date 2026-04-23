using AlbumList.Models;
using AlbumList.ViewModels;

namespace AlbumList.Views;

public partial class AlbumListPage : ContentPage
{
    public AlbumListViewModel ViewModel => (AlbumListViewModel)BindingContext;

    public AlbumListPage(AlbumListViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private void OnViewMenuItemClicked(object sender, EventArgs e)
    {
        if (sender is MenuFlyoutItem { BindingContext: Album album })
            ViewModel.ViewCommand.Execute(album);
    }

    private void OnDeleteMenuItemClicked(object sender, EventArgs e)
    {
        if (sender is MenuFlyoutItem { BindingContext: Album album })
            ViewModel.DeleteCommand.Execute(album);
    }
}
