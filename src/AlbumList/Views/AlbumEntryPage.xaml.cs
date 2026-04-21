using AlbumList.ViewModels;

namespace AlbumList.Views;

public partial class AlbumEntryPage : ContentPage
{
    public AlbumEntryPage(AlbumEntryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
