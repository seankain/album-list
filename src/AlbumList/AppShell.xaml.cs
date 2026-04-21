using AlbumList.Views;

namespace AlbumList;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("entry", typeof(AlbumEntryPage));
    }
}
