using AlbumList.Services;

namespace AlbumList;

public partial class App : Application
{
	private readonly MetadataBackgroundWorker _worker;

	public App(MetadataBackgroundWorker worker)
	{
		_worker = worker;
		InitializeComponent();
		MainPage = new AppShell();
	}

	protected override void OnResume()
	{
		base.OnResume();
		_worker.Start();
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		_worker.Stop();
	}
}
