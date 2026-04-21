using AlbumList.ViewModels;

namespace AlbumList.Views;

public partial class AddAlbumPage : ContentPage
{
    public AddAlbumPage(AddAlbumViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
