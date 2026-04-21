using AlbumList.Data;
using AlbumList.Services;
using AlbumList.ViewModels;
using AlbumList.Views;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace AlbumList;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AlbumDatabase>();
        builder.Services.AddHttpClient<IMetadataService, WikipediaMetadataService>();
        builder.Services.AddSingleton<MetadataBackgroundWorker>();
        builder.Services.AddSingleton<IExportService, CsvExportService>();
        builder.Services.AddSingleton<DatabaseExportService>();
        builder.Services.AddSingleton<IFileSaver>(
            new MauiFileSaverAdapter(CommunityToolkit.Maui.Storage.FileSaver.Default));
        builder.Services.AddSingleton(Connectivity.Current);

        builder.Services.AddTransient<AlbumListViewModel>();
        builder.Services.AddTransient<AddAlbumViewModel>();
        builder.Services.AddTransient<AlbumEntryViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        builder.Services.AddTransient<AlbumListPage>();
        builder.Services.AddTransient<AddAlbumPage>();
        builder.Services.AddTransient<AlbumEntryPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
