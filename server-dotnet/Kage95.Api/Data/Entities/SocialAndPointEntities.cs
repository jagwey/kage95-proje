namespace Kage95.Api.Data.Entities;

public static class FriendshipStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Blocked = "blocked";
}

public class Friendship
{
    public uint UserLow { get; set; }
    public uint UserHigh { get; set; }
    public uint RequestedBy { get; set; }
    public string Status { get; set; } = FriendshipStatuses.Pending;
    public uint? BlockedBy { get; set; }

    public User LowUser { get; set; } = null!;
    public User HighUser { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = [];
}

public class Message
{
    public uint Id { get; set; }
    public uint UserLow { get; set; }
    public uint UserHigh { get; set; }
    public uint SenderId { get; set; }
    public string? Body { get; set; }
    public uint? MangaId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Friendship Friendship { get; set; } = null!;
    public Manga? Manga { get; set; }
}

public class PointRecord
{
    public uint Id { get; set; }
    public uint UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int Points { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
