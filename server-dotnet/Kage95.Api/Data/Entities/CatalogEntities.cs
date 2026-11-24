namespace Kage95.Api.Data.Entities;

public class Manga
{
    public uint Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string JapaneseTitle { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string Status { get; set; } = "Devam Ediyor";
    public decimal CatalogRating { get; set; } = 8.0m;
    public uint Chapters { get; set; }
    public ushort Year { get; set; }
    public string Views { get; set; } = "0";
    public string Accent { get; set; } = "#68c58a";
    public DateTime CreatedAt { get; set; }

    public ICollection<Favorite> Favorites { get; set; } = [];
    public ICollection<Rating> Ratings { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<ReadingListEntry> ReadingListEntries { get; set; } = [];
    public ICollection<Chapter> ChapterItems { get; set; } = [];
}

public class Favorite
{
    public uint UserId { get; set; }
    public uint MangaId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Manga Manga { get; set; } = null!;
}

public class Rating
{
    public uint MangaId { get; set; }
    public uint UserId { get; set; }
    public byte Score { get; set; }

    public Manga Manga { get; set; } = null!;
    public User User { get; set; } = null!;
}

public class Comment
{
    public uint Id { get; set; }
    public uint MangaId { get; set; }
    public uint UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Manga Manga { get; set; } = null!;
    public User User { get; set; } = null!;
}
