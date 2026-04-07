# High level overview:
This is an android compatible dotnet MAUI app that allows users to create and maintain a list of music albums.

## Design details:
#### Permissions
The app needs no camera or microphone permissions and does not ask for them.

#### Storage
The list of albums is stored by the app in a local sqlite database.
The album table will have a schema that is the following:
Primary Key, Album name, Artist, Release Date, personal signifiance rating (0-10), critical signifiance rating (0-10) and a large text column for writing a rating summary.

#### Features
- There is an 'Add album' view that allows the user to put in the album title and artist.  
While the app is active there is a background worker that will fill in metadata gaps for the albums. For example, if the user does not add the year it will be retrieved via http from
wikipedia and populated.  
If the user puts in a redundant album they are notified and the album is deduplicated in the list.  
- The app has a 'View' page that lets the user view the list in a a clean and space efficient way. From this list there is a more-vert icon that will allow the user to delete
or view the individual entry for each album.  
It also allows the user to sort by year, artist or album name. The summary column is not visible from the list view.  
- The 'Entry' view for each album will show the subjective rating scores (personal and critical significance) as well as show the summary in an editable text field.  
There is a settings page that allows the user to dump the album list as a CSV or export the sqlite database directly.  
