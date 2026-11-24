namespace Kage95.Api.Data.Entities;

public static class UserRoles
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Admin = "admin";

    public static readonly string[] All = [User, Moderator, Admin];

    public static bool IsStaff(string? role) => role is Admin or Moderator;
}

public class User
{
    public uint Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Session> Sessions { get; set; } = [];
    public ICollection<Favorite> Favorites { get; set; } = [];
    public ICollection<Rating> Ratings { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
    public ICollection<ReadingListEntry> ReadingList { get; set; } = [];
    public ICollection<PointRecord> PointRecords { get; set; } = [];
}

public class Session
{
    public byte[] TokenHash { get; set; } = [];
    public uint UserId { get; set; }
    public DateTime ExpiresAt { get; set; }

    public User User { get; set; } = null!;
}
