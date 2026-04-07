# album-list Architecture

This document translates the high-level requirements in [`DESIGN.md`](./DESIGN.md) into a concrete architectural blueprint that can guide implementation.

## 1. Overview

`album-list` is a single-project **.NET MAUI** application targeting **Android** that lets a user maintain a personal catalog of music albums. It follows the **MVVM** pattern using [`CommunityToolkit.Mvvm`](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for source-generated `ObservableObject` / `RelayCommand` plumbing, and uses the built-in MAUI dependency injection container (`MauiAppBuilder.Services`) to wire everything together.

Guiding principles:

- **Local-first** — all user data lives in an on-device SQLite file; the network is only used opportunistically to enrich metadata.
- **Minimal permissions** — no camera, microphone, or location access. Only `INTERNET` is declared.
- **Reactive UI** — the views observe view-models; the background metadata worker raises events that cause the list to refresh automatically.

## 2. Solution / Project Layout

```
album-list/
├── DESIGN.md
├── ARCHITECTURE.md
├── AlbumList.sln
├── src/
│   └── AlbumList/                 # .NET MAUI app project
│       ├── App.xaml(.cs)
│       ├── AppShell.xaml(.cs)
│       ├── MauiProgram.cs
│       ├── Models/
│       │   └── Album.cs
│       ├── Data/
│       │   └── AlbumDatabase.cs
│       ├── Services/
│       │   ├── IMetadataService.cs
│       │   ├── WikipediaMetadataService.cs
│       │   ├── MetadataBackgroundWorker.cs
│       │   ├── IExportService.cs
│       │   ├── CsvExportService.cs
│       │   └── DatabaseExportService.cs
│       ├── ViewModels/
│       │   ├── AddAlbumViewModel.cs
│       │   ├── AlbumListViewModel.cs
│       │   ├── AlbumEntryViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Views/
│       │   ├── AddAlbumPage.xaml(.cs)
│       │   ├── AlbumListPage.xaml(.cs)
│       │   ├── AlbumEntryPage.xaml(.cs)
│       │   └── SettingsPage.xaml(.cs)
│       └── Platforms/Android/
│           ├── AndroidManifest.xml
│           └── MainActivity.cs
└── tests/
    └── AlbumList.Tests/           # xUnit test project
```

## 3. Data Layer

### 3.1 `Album` model

The `Album` POCO maps 1:1 to the schema in `DESIGN.md`:

| Column                 | CLR type    | Notes                                  |
| ---------------------- | ----------- | -------------------------------------- |
| `Id`                   | `int`       | `[PrimaryKey, AutoIncrement]`          |
| `Name`                 | `string`    | Required                               |
| `Artist`               | `string`    | Required                               |
| `ReleaseDate`          | `DateTime?` | Nullable — filled in by metadata worker|
| `PersonalRating`       | `int`       | 0–10, validated in VM                  |
| `CriticalRating`       | `int`       | 0–10, validated in VM                  |
| `Summary`              | `string?`   | Large free-form text                   |

### 3.2 `AlbumDatabase`

Wraps `sqlite-net-pcl` (`SQLiteAsyncConnection`). The database file lives at `Path.Combine(FileSystem.AppDataDirectory, "albums.db3")`. On first use it calls `CreateTableAsync<Album>()` and creates a unique index for case-insensitive deduplication:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS ux_album_name_artist
  ON Album (Name COLLATE NOCASE, Artist COLLATE NOCASE);
```

Async repository methods exposed to view-models and services:

- `Task<List<Album>> GetAllAsync(AlbumSort sort)`
- `Task<Album?> GetAsync(int id)`
- `Task<Album?> FindDuplicateAsync(string name, string artist)`
- `Task<int> UpsertAsync(Album album)`
- `Task<int> DeleteAsync(int id)`
- `Task<List<Album>> GetIncompleteAsync()` — used by the background worker.

`AlbumSort` is an enum: `Name`, `Artist`, `Year`.

## 4. Background Metadata Worker

`MetadataBackgroundWorker` is a long-running task (not an Android `Service` — keeping it in-process avoids extra permissions).

- **Lifecycle** — started from `App.OnResume`, cancelled via a `CancellationTokenSource` in `App.OnSleep`. The MAUI `IConnectivity` API is consulted before each network call; offline state pauses the loop with exponential backoff (1s → 30s cap).
- **Inputs** — a `Channel<int>` of newly inserted album IDs (written by `AddAlbumViewModel`) plus a periodic sweep (`AlbumDatabase.GetIncompleteAsync`) every 5 minutes for any rows still missing fields.
- **Enrichment flow**
  1. Build a query of `"{Album} {Artist} album"`.
  2. Call the Wikipedia search API to resolve a page title.
  3. Call `https://en.wikipedia.org/api/rest_v1/page/summary/{title}` and parse the description / extract for the release year.
  4. Persist the updated `Album` via `UpsertAsync`.
- **Notification** — raises a `WeakReferenceMessenger` (`CommunityToolkit.Mvvm.Messaging`) message that `AlbumListViewModel` subscribes to in order to refresh the list.

`IMetadataService` is the seam for unit tests; `WikipediaMetadataService` is the production implementation and is constructed with an injected `HttpClient` registered via `services.AddHttpClient<WikipediaMetadataService>()`.

## 5. View-Models & Views

Navigation uses MAUI **Shell** with three top-level tabs:

| Route        | Page             | View-Model            |
| ------------ | ---------------- | --------------------- |
| `//list`     | `AlbumListPage`  | `AlbumListViewModel`  |
| `//add`      | `AddAlbumPage`   | `AddAlbumViewModel`   |
| `//settings` | `SettingsPage`   | `SettingsViewModel`   |
| `entry`      | `AlbumEntryPage` | `AlbumEntryViewModel` |

`AlbumEntryPage` is pushed onto the navigation stack from the list (it is not a tab).

### 5.1 `AddAlbumViewModel`

- Two-way bound `Name` and `Artist` strings.
- `SaveCommand` calls `AlbumDatabase.FindDuplicateAsync` first; if a duplicate exists it raises a `DisplayAlert` ("Album already in your list") and navigates to that entry instead of inserting.
- On insert it pushes the new ID into the metadata worker's `Channel<int>`.

### 5.2 `AlbumListViewModel`

- Holds `ObservableCollection<Album> Albums` and `AlbumSort SelectedSort`.
- `SortCommand(AlbumSort)` reloads from the database.
- The view uses a `CollectionView` with a per-item context menu (`MenuFlyout` reached via the more-vert icon) exposing **View** and **Delete** actions. The `Summary` column is intentionally not bound in this view.
- Subscribes to the metadata-worker message to refresh after enrichment.

### 5.3 `AlbumEntryViewModel`

- Loads a single `Album` by query parameter.
- Two `Slider` controls (range 0–10, integer snap) bound to `PersonalRating` and `CriticalRating`.
- `Editor` bound to `Summary`.
- `SaveCommand` persists changes; navigation back triggers a refresh on the list.

### 5.4 `SettingsViewModel`

- `ExportCsvCommand` → `CsvExportService.ExportAsync()` → `IFileSaver.SaveAsync(...)`.
- `ExportDatabaseCommand` → `DatabaseExportService.ExportAsync()` copies the raw `albums.db3` file via `IFileSaver`.

## 6. Export Services

- `CsvExportService` serializes all rows including `Summary`. It uses a tiny hand-rolled writer (no extra package) that quotes fields containing commas, quotes, or newlines.
- `DatabaseExportService` copies the SQLite file from `FileSystem.AppDataDirectory` to a user-chosen location using `CommunityToolkit.Maui.Storage.IFileSaver`.

Both implementations live behind `IExportService` for testability.

## 7. Permissions & Platform Configuration

`Platforms/Android/AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.INTERNET" />
```

No camera, microphone, location, or storage permissions are declared (the file saver uses the system document picker, which does not require runtime permissions on modern Android).

## 8. Dependency Injection (`MauiProgram.cs`)

```csharp
builder.Services.AddSingleton<AlbumDatabase>();
builder.Services.AddHttpClient<IMetadataService, WikipediaMetadataService>();
builder.Services.AddSingleton<MetadataBackgroundWorker>();
builder.Services.AddSingleton<IExportService, CsvExportService>();
builder.Services.AddSingleton<DatabaseExportService>();
builder.Services.AddSingleton(FileSaver.Default);
builder.Services.AddSingleton(Connectivity.Current);

builder.Services.AddTransient<AlbumListViewModel>();
builder.Services.AddTransient<AddAlbumViewModel>();
builder.Services.AddTransient<AlbumEntryViewModel>();
builder.Services.AddTransient<SettingsViewModel>();

builder.Services.AddTransient<AlbumListPage>();
builder.Services.AddTransient<AddAlbumPage>();
builder.Services.AddTransient<AlbumEntryPage>();
builder.Services.AddTransient<SettingsPage>();
```

## 9. Testing Strategy

`tests/AlbumList.Tests` is an xUnit project with:

- **Repository tests** — `AlbumDatabase` exercised against an in-memory SQLite connection (`":memory:"`), covering insert, dedupe, sort, delete.
- **Metadata parser tests** — `WikipediaMetadataService` exercised with a fake `HttpMessageHandler` returning canned JSON payloads, asserting correct year extraction and graceful failure on malformed input.
- **Export tests** — `CsvExportService` round-trips a small album set and asserts proper escaping of commas, quotes, and newlines inside `Summary`.

The view-models are kept thin; behavior tests live in the service/repository layers.

## 10. Build & CI

- Local build: `dotnet build -t:Run -f net8.0-android src/AlbumList/AlbumList.csproj`.
- Tests: `dotnet test`.
- No CI is configured yet; a future GitHub Actions workflow can run `dotnet build` and `dotnet test` on PRs.

## 11. Requirement Traceability

| `DESIGN.md` requirement                                          | Component                                                                 |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------- |
| Android-compatible .NET MAUI app                                 | `src/AlbumList` MAUI project, `net8.0-android` target                     |
| No camera or microphone permissions                              | `Platforms/Android/AndroidManifest.xml` (only `INTERNET`)                 |
| Local SQLite storage with the specified album schema             | `Models/Album.cs`, `Data/AlbumDatabase.cs`                                |
| Add-album view with album/artist input                           | `Views/AddAlbumPage.xaml`, `AddAlbumViewModel`                            |
| Background worker fills metadata gaps from Wikipedia             | `Services/MetadataBackgroundWorker.cs`, `Services/WikipediaMetadataService.cs` |
| Duplicate detection / dedupe notification                        | `AlbumDatabase.FindDuplicateAsync`, `AddAlbumViewModel.SaveCommand`       |
| List view with sort by year/artist/name and more-vert actions    | `Views/AlbumListPage.xaml`, `AlbumListViewModel`                          |
| Summary column hidden from list                                  | `AlbumListPage` data template (no `Summary` binding)                      |
| Per-entry view showing ratings + editable summary                | `Views/AlbumEntryPage.xaml`, `AlbumEntryViewModel`                        |
| Settings page with CSV and SQLite export                         | `Views/SettingsPage.xaml`, `CsvExportService`, `DatabaseExportService`    |
