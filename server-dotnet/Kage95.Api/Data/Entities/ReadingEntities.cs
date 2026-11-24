namespace Kage95.Api.Data.Entities;

public static class ReadingStatuses
{
    public const string Reading = "reading";
    public const string Completed = "completed";
    public const string PlanToRead = "plan_to_read";
    public const string Dropped = "dropped";

    public static readonly string[] All = [Reading, Completed, PlanToRead, Dropped];
}

public class ReadingListEntry
{
    public uint UserId { get; set; }
    public uint MangaId { get; set; }
    public string Status { get; set; } = ReadingStatuses.Reading;
    public uint CurrentChapter { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Manga Manga { get; set; } = null!;
}

public class Chapter
{
    public uint Id { get; set; }
    public uint MangaId { get; set; }
    public decimal ChapterNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Manga Manga { get; set; } = null!;
    public ICollection<ChapterPage> Pages { get; set; } = [];
}

public class ChapterPage
{
    public uint Id { get; set; }
    public uint ChapterId { get; set; }
    public uint PageNumber { get; set; }
    public string ImageUrl { get; set; } = string.Empty;

    public Chapter Chapter { get; set; } = null!;
}
