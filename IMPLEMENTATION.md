# album-list Implementation Plan

This document breaks the architecture defined in [`ARCHITECTURE.md`](./ARCHITECTURE.md) into incremental, manageable steps sized for a Claude Sonnet 4.6 coding session. Each step is independently buildable / testable and ends with its own commit so diffs stay small and reviewable.

---

## Step 1 — Solution & project scaffolding

**Output:** an empty but buildable solution.

- `dotnet new sln -n AlbumList` at the repo root.
- `dotnet new maui -n AlbumList -o src/AlbumList`, then `dotnet sln add src/AlbumList/AlbumList.csproj`.
- Edit `src/AlbumList/AlbumList.csproj`:
  - Trim `TargetFrameworks` to `net8.0-android` only.
  - `ApplicationId=com.seankain.albumlist`, `ApplicationTitle=Album List`.
- Add NuGet packages:
  - `sqlite-net-pcl`
  - `SQLitePCLRaw.bundle_green`
  - `CommunityToolkit.Mvvm`
  - `CommunityToolkit.Maui` (provides `IFileSaver`)
- Register `.UseMauiCommunityToolkit()` inside `MauiProgram.CreateMauiApp()`.
- `dotnet new xunit -o tests/AlbumList.Tests`; add to the solution and reference the MAUI project.

**Verify:** `dotnet build` succeeds for both projects.
**Commit:** `Scaffold MAUI solution and test project`.

---

## Step 2 — Data layer

**Output:** persistent CRUD with case-insensitive deduplication, fully unit-tested.

- `src/AlbumList/Models/Album.cs` — schema from `ARCHITECTURE.md` §3.1 with `sqlite-net` attributes (`[PrimaryKey, AutoIncrement]`, etc.).
- `src/AlbumList/Data/AlbumSort.cs` — enum (`Name`, `Artist`, `Year`).
- `src/AlbumList/Data/AlbumDatabase.cs`
  - Lazy-initialized `SQLiteAsyncConnection` at `Path.Combine(FileSystem.AppDataDirectory, "albums.db3")`.
  - `InitAsync` calls `CreateTableAsync<Album>()` and creates the unique index from §3.2:
    ```sql
    CREATE UNIQUE INDEX IF NOT EXISTS ux_album_name_artist
      ON Album (Name COLLATE NOCASE, Artist COLLATE NOCASE);
    ```
  - Methods: `GetAllAsync(AlbumSort)`, `GetAsync`, `FindDuplicateAsync`, `UpsertAsync`, `DeleteAsync`, `GetIncompleteAsync`.
  - Constructor accepts an optional `string?` db path so tests can pass `":memory:"`.
- `tests/AlbumList.Tests/AlbumDatabaseTests.cs` — covers insert, sort by each field, case-insensitive dedupe, delete, and that `GetIncompleteAsync` only returns rows missing `ReleaseDate`.

**Verify:** `dotnet test` passes.
**Commit:** `Add Album model and SQLite repository`.

---

## Step 3 — Wikipedia metadata service

**Output:** an HTTP-bound service with unit tests using a fake `HttpMessageHandler`.

- `src/AlbumList/Services/IMetadataService.cs`
  - `Task<AlbumMetadata?> LookupAsync(string name, string artist, CancellationToken ct)`
  - `AlbumMetadata` record carries `DateTime? ReleaseDate`.
- `src/AlbumList/Services/WikipediaMetadataService.cs`
  - Constructor takes `HttpClient`.
  - **Step 1**: `GET https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Album}+{Artist}+album&format=json`, take the first hit's title.
  - **Step 2**: `GET https://en.wikipedia.org/api/rest_v1/page/summary/{title}`, parse the `extract` for a 4-digit year via `Regex(@"\b(19|20)\d{2}\b")`.
  - Returns `null` on any failure (network, parse, no hit). Never throws.
- `tests/AlbumList.Tests/WikipediaMetadataServiceTests.cs` — uses a `FakeHttpMessageHandler` returning canned JSON for the happy path, no search hits, and a malformed extract.

**Verify:** `dotnet test` passes.
**Commit:** `Add Wikipedia metadata lookup service`.

---

## Step 4 — Background metadata worker

**Output:** a singleton `MetadataBackgroundWorker` driven by a queue.

- `src/AlbumList/Messages/AlbumsUpdatedMessage.cs` — empty record.
- `src/AlbumList/Services/MetadataBackgroundWorker.cs`
  - Singleton; holds a `Channel<int>` for newly inserted album IDs and a `CancellationTokenSource`.
  - `Start()` launches a `Task.Run` loop that:
    1. Drains the channel (with a 5-minute timeout fallback that calls `AlbumDatabase.GetIncompleteAsync`).
    2. For each album with missing `ReleaseDate`, calls `IMetadataService.LookupAsync`, merges the result, and persists via `UpsertAsync`.
    3. Sends `WeakReferenceMessenger.Default.Send(new AlbumsUpdatedMessage())`.
    4. Skips network calls when `IConnectivity.NetworkAccess != Internet`; backs off exponentially up to 30 s on failures.
  - `Stop()` cancels the token.
- Hook into `App.xaml.cs`: `OnResume` → `worker.Start()`, `OnSleep` → `worker.Stop()`.

**Verify:** `dotnet build` succeeds (no new tests this step).
**Commit:** `Add background metadata worker`.

---

## Step 5 — Export services

**Output:** CSV and raw-DB exporters behind `IExportService`.

- `src/AlbumList/Services/IExportService.cs` — `Task ExportAsync(CancellationToken ct)`.
- `src/AlbumList/Services/CsvExportService.cs`
  - Pulls all albums via `AlbumDatabase.GetAllAsync(AlbumSort.Name)`.
  - Hand-rolled CSV writer that quotes any field containing `,`, `"`, `\r`, or `\n`, doubling internal quotes.
  - Writes to a `MemoryStream` and hands it to the injected `IFileSaver` as `albums.csv`.
- `src/AlbumList/Services/DatabaseExportService.cs`
  - Opens a `FileStream` on `albums.db3` and passes it to `IFileSaver` as `albums.db3`.
- `tests/AlbumList.Tests/CsvExportServiceTests.cs` — uses a fake `IFileSaver` that captures the written stream and asserts proper escaping (commas, quotes, newlines in `Summary`).

**Verify:** `dotnet test` passes.
**Commit:** `Add CSV and database export services`.

---

## Step 6 — View-models

**Output:** four `ObservableObject` view-models using `[ObservableProperty]` / `[RelayCommand]`. No XAML yet.

- `src/AlbumList/ViewModels/AddAlbumViewModel.cs` — `Name`, `Artist`; `SaveCommand` (dedupe → notify and navigate to existing entry, or insert → push ID into worker channel → navigate back).
- `src/AlbumList/ViewModels/AlbumListViewModel.cs` — `ObservableCollection<Album> Albums`, `SelectedSort`, `LoadCommand`, `SortCommand`, `DeleteCommand(Album)`, `ViewCommand(Album)` (navigates to `entry` route). Subscribes to `AlbumsUpdatedMessage` in the constructor.
- `src/AlbumList/ViewModels/AlbumEntryViewModel.cs` — `[QueryProperty]` for `Id`; loads the album, exposes `Name`, `Artist`, `ReleaseDate`, `PersonalRating`, `CriticalRating`, `Summary`; `SaveCommand`.
- `src/AlbumList/ViewModels/SettingsViewModel.cs` — `ExportCsvCommand`, `ExportDatabaseCommand`.

**Verify:** `dotnet build` succeeds. (Optional: a smoke test for `AlbumListViewModel.LoadCommand` against an in-memory DB.)
**Commit:** `Add view-models`.

---

## Step 7 — Views, Shell navigation, DI wiring

**Output:** runnable app with all four pages.

- `src/AlbumList/AppShell.xaml` — three `ShellContent` tabs (`//list`, `//add`, `//settings`); `Routing.RegisterRoute("entry", typeof(AlbumEntryPage))` in the code-behind.
- `Views/AlbumListPage.xaml` — `CollectionView` bound to `Albums`; `DataTemplate` showing Name / Artist / Year; per-item more-vert `ImageButton` opening a `MenuFlyout` with **View** and **Delete** items bound to commands; sort `Picker` at the top.
- `Views/AddAlbumPage.xaml` — two `Entry` controls and a Save `Button`.
- `Views/AlbumEntryPage.xaml` — read-only Name / Artist labels, two `Slider`s (0–10, integer snap) with value labels, multi-line `Editor` for `Summary`, Save button.
- `Views/SettingsPage.xaml` — two buttons: Export CSV, Export Database.
- `MauiProgram.cs` — DI registrations exactly as in `ARCHITECTURE.md` §8 (singletons for `AlbumDatabase`, `MetadataBackgroundWorker`, `IFileSaver`, `IConnectivity`; `AddHttpClient<IMetadataService, WikipediaMetadataService>`; transients for view-models and pages).
- `Platforms/Android/AndroidManifest.xml` — only `<uses-permission android:name="android.permission.INTERNET" />`. Strip any default camera/mic/storage permissions emitted by the template.

**Verify:** `dotnet build -f net8.0-android` succeeds.
**Commit:** `Add views, Shell navigation, and DI wiring`.

---

## Step 8 — Final verification & push

- `dotnet build` and `dotnet test` from the repo root — both green.
- Walk every row of the §11 traceability table in `ARCHITECTURE.md` and confirm each maps to a real file.
- `git push -u origin claude/architecture-plan-design-gKmpL`. **Do not** open a pull request — the user has not requested one.

---

## Files to be Created

- `AlbumList.sln`
- `src/AlbumList/AlbumList.csproj`
- `src/AlbumList/MauiProgram.cs`
- `src/AlbumList/App.xaml{,.cs}`, `AppShell.xaml{,.cs}`
- `src/AlbumList/Models/Album.cs`
- `src/AlbumList/Data/{AlbumSort.cs, AlbumDatabase.cs}`
- `src/AlbumList/Services/{IMetadataService.cs, WikipediaMetadataService.cs, MetadataBackgroundWorker.cs, IExportService.cs, CsvExportService.cs, DatabaseExportService.cs}`
- `src/AlbumList/Messages/AlbumsUpdatedMessage.cs`
- `src/AlbumList/ViewModels/{AddAlbumViewModel.cs, AlbumListViewModel.cs, AlbumEntryViewModel.cs, SettingsViewModel.cs}`
- `src/AlbumList/Views/{AddAlbumPage.xaml{,.cs}, AlbumListPage.xaml{,.cs}, AlbumEntryPage.xaml{,.cs}, SettingsPage.xaml{,.cs}}`
- `src/AlbumList/Platforms/Android/AndroidManifest.xml`
- `tests/AlbumList.Tests/AlbumList.Tests.csproj`
- `tests/AlbumList.Tests/{AlbumDatabaseTests.cs, WikipediaMetadataServiceTests.cs, CsvExportServiceTests.cs}`

## End-to-End Verification

1. `dotnet build` at the repo root completes cleanly.
2. `dotnet test` reports all xUnit tests passing (database, Wikipedia parser, CSV export).
3. `AndroidManifest.xml` declares only `INTERNET`.
4. Every row of `ARCHITECTURE.md` §11 maps to a real file in the tree.
5. `git status` is clean; `git log` shows the eight incremental commits; `git push` succeeds on `claude/architecture-plan-design-gKmpL`.

## Notes for the Implementer

- Treat each numbered step as a single self-contained task: read the relevant section of `ARCHITECTURE.md`, write the files, build/test, commit, then move on. Do not collapse multiple steps into one commit — small commits keep diffs reviewable and let CI (when added later) pinpoint regressions.
- Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`) instead of hand-written `INotifyPropertyChanged` plumbing.
- Do **not** create a pull request. Push only.
