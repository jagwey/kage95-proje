namespace Kage95.Api.DTOs;

// --- Auth & User ---
public record UserDto(
    uint Id,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? Bio = null,
    string? AvatarUrl = null
);

public record AuthMeResponse(UserDto? User, int PointsEarned = 0);
public record AuthResponse(UserDto User, int PointsEarned = 0);

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Username,
    string Password
);

public record LoginRequest(string Login, string Password);

// --- Profile (Hesabım) ---
public record PointHistoryItemDto(
    uint Id,
    string Action,
    string Label,
    int Points,
    string? Note,
    DateTime CreatedAt
);

public record ProfileStatsDto(
    int FavoritesCount,
    int ReadingCount,
    int CompletedCount,
    int RatingsCount,
    int CommentsCount,
    int FriendsCount
);

public record MyProfileDto(
    uint Id,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    string? Bio,
    string? AvatarUrl,
    DateTime CreatedAt,
    LeaderboardMeDto Rank,
    ProfileStatsDto Stats,
    List<PointHistoryItemDto> RecentPoints
);

public record PublicProfileDto(
    uint Id,
    string Username,
    string Role,
    string? Bio,
    string? AvatarUrl,
    DateTime CreatedAt,
    int? Rank,
    int TotalPoints,
    int Level,
    string Title,
    ProfileStatsDto Stats
);

public record UpdateProfileRequest(
    string FirstName,
    string LastName,
    string? Bio,
    string? AvatarUrl
);

// --- Leaderboard (Liderlik) ---
public record LeaderboardRowDto(
    int Rank,
    uint UserId,
    string Username,
    string Role,
    string? AvatarUrl,
    int Points,
    int Level,
    string Title
);

public record LeaderboardMeDto(
    int? Rank,
    int Points,
    int TotalPoints,
    int Level,
    string Title,
    int LevelRequired,
    int NextLevelRemaining,
    string? NextTitle,
    int NextTitleRemaining
);

public record PointRuleDto(string Action, string Name, int Points, string Rule);

public record LeaderboardResponseDto(
    string Period,
    DateTime UpdatedAt,
    List<LeaderboardRowDto> List,
    LeaderboardMeDto? Me,
    List<PointRuleDto> Rules,
    int DailyCap
);

// --- Catalog, Discussion, Favorites ---
public record MangaDto(
    uint Id,
    string Title,
    string JapaneseTitle,
    string Author,
    List<string> Genres,
    string Summary,
    string Cover,
    string Status,
    decimal Rating,
    int RatingCount,
    uint Chapters,
    int UploadedChapters,
    ushort Year,
    string Views,
    string Accent
);

public record FavoritesBatchRequest(List<uint> Ids);
public record RatingRequest(int Score);
public record CommentRequest(string Body);

public record CommentDto(
    uint Id,
    uint UserId,
    string Username,
    string Role,
    string Body,
    DateTime CreatedAt
);

public record RatingSummaryDto(decimal? Average, int Count, int? Mine);

public record DiscussionDto(
    RatingSummaryDto Rating,
    List<CommentDto> Comments,
    bool HasMore
);

public record ActionResultDto(int PointsEarned = 0);

// --- Reading List & Chapters ---
public record ReadingListItemDto(
    uint MangaId,
    string Title,
    string Cover,
    uint TotalChapters,
    string Status,
    uint CurrentChapter,
    DateTime UpdatedAt
);

public record UpsertReadingListRequest(string Status, uint CurrentChapter);

public record ChapterSummaryDto(
    uint Id,
    uint MangaId,
    decimal ChapterNumber,
    string Title,
    int PageCount,
    DateTime CreatedAt
);

public record ChapterPageDto(uint Id, uint PageNumber, string ImageUrl);

public record ChapterDetailDto(
    uint Id,
    uint MangaId,
    string MangaTitle,
    decimal ChapterNumber,
    string Title,
    DateTime CreatedAt,
    List<ChapterPageDto> Pages,
    uint? PreviousChapterId,
    uint? NextChapterId
);

// --- Social (Friends, Blocks, Chat) ---
public record ProfileSummaryDto(uint Id, string Username, string Role);

public record FriendDto(
    uint Id,
    string Username,
    string Role,
    string Status,
    uint RequestedBy
);

public record BlockedUserDto(uint Id, string Username, string Role);

public record MessageDto(
    uint Id,
    uint SenderId,
    string? Body,
    uint? MangaId,
    string? MangaTitle,
    DateTime CreatedAt
);

public record MessageHistoryDto(List<MessageDto> Messages, bool HasMore);
public record SendMessageRequest(string? Body, uint? MangaId);

// --- Admin & Moderation ---
public record AdminStatsDto(
    int Users,
    int Mangas,
    int Chapters,
    int Comments,
    int Ratings,
    int Messages
);

public record AdminUserDto(
    uint Id,
    string Username,
    string FirstName,
    string LastName,
    string Email,
    string Role,
    int TotalPoints,
    int Level,
    string Title,
    DateTime CreatedAt
);

public record UpdateUserRoleRequest(string Role);
public record AdjustUserPointsRequest(int Points, string? Note);

public record UpsertMangaRequest(
    string Title,
    string JapaneseTitle,
    string Author,
    List<string> Genres,
    string Summary,
    string Cover,
    string Status,
    decimal Rating,
    uint Chapters,
    ushort Year,
    string Views,
    string Accent
);

public record CreateChapterRequest(
    decimal ChapterNumber,
    string Title,
    List<string> PageUrls
);

public record UploadResultDto(string Url);
