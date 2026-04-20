using SQLite;

namespace AlbumList.Models;

public class Album
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public int PersonalRating { get; set; }
    public int CriticalRating { get; set; }
    public string? Summary { get; set; }
}
