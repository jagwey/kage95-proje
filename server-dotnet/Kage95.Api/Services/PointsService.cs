using System.Globalization;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Services;

/// <summary>
/// jagwey/manga-web projesindeki PuanServisi ve Liderlik sisteminin KAGE95 MySQL + EF Core uyarlaması.
/// Seviye formülü: 50*(L-1)*L (maks. 95). Rütbeler: Çırak -> Genç Gölge -> Gölge -> Usta -> Efsane.
/// </summary>
public class PointsService(Kage95DbContext db)
{
    public const string ActionChapterRead = "bolum_oku";
    public const string ActionComment = "yorum";
    public const string ActionRate = "puan_ver";
    public const string ActionLibraryAdd = "kutuphane_ekle";
    public const string ActionDailyLogin = "gunluk_giris";
    public const string ActionStreak = "seri";
    public const string ActionAvatar = "profil_resmi";
    public const string ActionAdminBonus = "yonetici_bonus";
    public const string ActionAdminPenalty = "yonetici_ceza";

    public const int PointsChapterRead = 10;
    public const int PointsComment = 5;
    public const int PointsRate = 2;
    public const int PointsLibraryAdd = 3;
    public const int PointsDailyLogin = 2;
    public const int PointsStreak = 10;
    public const int StreakDays = 7;
    public const int PointsAvatar = 10;

    public const int DailyChapterLimit = 10;
    public const int DailyCommentLimit = 5;
    public const int DailyRateLimit = 10;
    public const int DailyLibraryLimit = 5;
    public const int DailyCap = 250;
    public const int MinCommentLength = 20;
    public const int MinCommentDistinctChars = 6;
    public const int MaxLevel = 95;

    private sealed record RuleDef(int Points, bool OncePerRef, int DailyLimit);

    private static readonly Dictionary<string, RuleDef> Rules = new()
    {
        [ActionChapterRead] = new(PointsChapterRead, true, DailyChapterLimit),
        [ActionComment] = new(PointsComment, true, DailyCommentLimit),
        [ActionRate] = new(PointsRate, true, DailyRateLimit),
        [ActionLibraryAdd] = new(PointsLibraryAdd, true, DailyLibraryLimit),
        [ActionDailyLogin] = new(PointsDailyLogin, true, 0),
        [ActionStreak] = new(PointsStreak, true, 0),
        [ActionAvatar] = new(PointsAvatar, true, 0),
    };

    public static readonly (int Level, string Title)[] Ranks =
    [
        (1, "Çırak"),
        (3, "Genç Gölge"),
        (6, "Gölge"),
        (12, "Usta"),
        (25, "Efsane"),
    ];

    private static readonly TimeZoneInfo IstanbulTz = ResolveIstanbulTimeZone();

    private static TimeZoneInfo ResolveIstanbulTimeZone()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("TR", TimeSpan.FromHours(3), "TR", "TR");
    }

    public static DateTime LocalNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstanbulTz);

    public static DateTime LocalDayStartUtc(DateTime localDate)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), IstanbulTz);

    private static string DayKey(DateTime localDate) => localDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static int PointsForLevel(int level) => 50 * (Math.Max(1, level) - 1) * Math.Max(1, level);

    public static int ComputeLevel(int totalPoints)
    {
        var safeTotal = Math.Max(0, totalPoints);
        var level = 1;
        while (level < MaxLevel && safeTotal >= PointsForLevel(level + 1)) level++;
        return level;
    }

    public static int RemainingForNextLevel(int totalPoints)
    {
        var safeTotal = Math.Max(0, totalPoints);
        var level = ComputeLevel(safeTotal);
        return level >= MaxLevel ? 0 : Math.Max(0, PointsForLevel(level + 1) - safeTotal);
    }

    public static string ComputeTitle(int totalPoints)
    {
        var level = ComputeLevel(totalPoints);
        var title = Ranks[0].Title;
        foreach (var rank in Ranks)
        {
            if (level >= rank.Level) title = rank.Title;
        }
        return title;
    }

    public static (string? NextTitle, int Remaining) ComputeNextTitle(int totalPoints)
    {
        var safeTotal = Math.Max(0, totalPoints);
        var level = ComputeLevel(safeTotal);
        foreach (var rank in Ranks)
        {
            if (level < rank.Level)
                return (rank.Title, Math.Max(0, PointsForLevel(rank.Level) - safeTotal));
        }
        return (null, 0);
    }

    public static string ActionLabel(string action) => action switch
    {
        ActionChapterRead => "Bölüm okudun",
        ActionComment => "Yorum yaptın",
        ActionRate => "Puan verdin",
        ActionLibraryAdd => "Kütüphaneye ekledin",
        ActionDailyLogin => "Günlük giriş",
        ActionStreak => "7 günlük seri bonusu",
        ActionAvatar => "Profil resmi ekledin",
        ActionAdminBonus => "Yönetici bonusu",
        ActionAdminPenalty => "Yönetici düzeltmesi",
        _ => action
    };

    public static List<PointRuleDto> GetRuleList() =>
    [
        new(ActionChapterRead, "Bölüm oku", PointsChapterRead, $"Bölümü tamamla. Bölüm başına 1 kez, günde en fazla {DailyChapterLimit} bölüm."),
        new(ActionComment, "Yorum yap", PointsComment, $"En az {MinCommentLength} karakterlik benzersiz yorum. Günde en fazla {DailyCommentLimit} yorum."),
        new(ActionRate, "Puan ver", PointsRate, $"Bir mangaya ilk kez puan ver. Seri başına 1 kez, günde en fazla {DailyRateLimit} oy."),
        new(ActionLibraryAdd, "Kütüphaneye ekle", PointsLibraryAdd, $"Favoriye veya okuma listesine ekle. Seri başına 1 kez, günde en fazla {DailyLibraryLimit} seri."),
        new(ActionDailyLogin, "Günlük giriş", PointsDailyLogin, "Her gün siteye girdiğinde 1 kez."),
        new(ActionStreak, "Seri bonusu", PointsStreak, $"{StreakDays} gün üst üste giriş yaptığında."),
        new(ActionAvatar, "Profil resmi", PointsAvatar, "Profil resmi eklediğinde 1 kez."),
    ];

    public async Task<int> AwardDailyLoginAsync(uint userId)
    {
        try
        {
            var today = LocalNow();
            var key = "gun:" + DayKey(today);
            var exists = await db.PointRecords.AsNoTracking()
                .AnyAsync(p => p.UserId == userId && p.Action == ActionDailyLogin && p.Reference == key);
            if (exists) return 0;

            var earned = await WriteRulePointsAsync(userId, ActionDailyLogin, key);
            if (earned <= 0) return 0;

            var days = await db.PointRecords.AsNoTracking()
                .Where(p => p.UserId == userId && p.Action == ActionDailyLogin && p.Reference != null)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => p.Reference!)
                .Take(400)
                .ToListAsync();

            var set = new HashSet<string>(days);
            var streak = 0;
            for (var d = today.Date; set.Contains("gun:" + DayKey(d)); d = d.AddDays(-1))
                streak++;

            if (streak > 0 && streak % StreakDays == 0)
            {
                earned += await WriteRulePointsAsync(userId, ActionStreak, "seri:" + DayKey(today));
            }
            return earned;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int> AwardAsync(uint userId, string action, string reference)
    {
        try
        {
            if (userId == 0 || !Rules.ContainsKey(action)) return 0;
            var bonus = await AwardDailyLoginAsync(userId);
            var earned = await WriteRulePointsAsync(userId, action, reference);
            return bonus + earned;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<int> AwardCommentAsync(uint userId, uint commentId, string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length < MinCommentLength || trimmed.Distinct().Count() < MinCommentDistinctChars)
            return 0;

        var duplicate = await db.Comments.AsNoTracking()
            .AnyAsync(c => c.UserId == userId && c.Id != commentId && c.Body == trimmed);
        if (duplicate) return 0;

        return await AwardAsync(userId, ActionComment, $"yorum:{commentId}");
    }

    public async Task RemoveCommentPointsAsync(uint commentId)
    {
        try
        {
            var reference = $"yorum:{commentId}";
            var records = await db.PointRecords
                .Where(p => p.Action == ActionComment && p.Reference == reference)
                .ToListAsync();
            if (records.Count > 0)
            {
                db.PointRecords.RemoveRange(records);
                await db.SaveChangesAsync();
            }
        }
        catch { }
    }

    public async Task<int> AdjustByAdminAsync(uint targetUserId, int points, string? note)
    {
        var action = points >= 0 ? ActionAdminBonus : ActionAdminPenalty;
        db.PointRecords.Add(new PointRecord
        {
            UserId = targetUserId,
            Action = action,
            Points = points,
            Reference = $"admin:{DateTime.UtcNow.Ticks}",
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return await GetTotalPointsAsync(targetUserId);
    }

    public async Task<int> GetTotalPointsAsync(uint userId)
        => await db.PointRecords.AsNoTracking()
            .Where(p => p.UserId == userId && p.Points != 0)
            .SumAsync(p => (int?)p.Points) ?? 0;

    public async Task<LeaderboardResponseDto> GetLeaderboardAsync(string? periodRaw, int limit, uint? currentUserId)
    {
        var period = (periodRaw ?? "tum").Trim().ToLowerInvariant();
        if (period is not ("hafta" or "ay" or "tum"))
            period = "tum";
        limit = Math.Clamp(limit, 1, 100);

        DateTime? startUtc = null;
        var localNow = LocalNow();
        if (period == "hafta")
        {
            var daysSinceMonday = ((int)localNow.DayOfWeek + 6) % 7;
            startUtc = LocalDayStartUtc(localNow.Date.AddDays(-daysSinceMonday));
        }
        else if (period == "ay")
        {
            startUtc = LocalDayStartUtc(new DateTime(localNow.Year, localNow.Month, 1));
        }

        var query = db.PointRecords.AsNoTracking().Where(p => p.Points != 0);
        if (startUtc.HasValue)
            query = query.Where(p => p.CreatedAt >= startUtc.Value);

        var ranked = await query
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Points = g.Sum(x => x.Points), LastAt = g.Max(x => x.CreatedAt) })
            .Where(x => x.Points > 0)
            .OrderByDescending(x => x.Points)
            .ThenBy(x => x.LastAt)
            .ThenBy(x => x.UserId)
            .ToListAsync();

        var rankLookup = new Dictionary<uint, (int Rank, int Points)>();
        for (var i = 0; i < ranked.Count; i++)
            rankLookup[ranked[i].UserId] = (i + 1, ranked[i].Points);

        var top = ranked.Take(limit).ToList();
        var topIds = top.Select(x => x.UserId).ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => topIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.Role, u.AvatarUrl })
            .ToDictionaryAsync(u => u.Id);

        Dictionary<uint, int> allTimeTotals;
        if (!startUtc.HasValue)
        {
            allTimeTotals = top.ToDictionary(x => x.UserId, x => x.Points);
        }
        else
        {
            allTimeTotals = await db.PointRecords.AsNoTracking()
                .Where(p => p.Points != 0 && topIds.Contains(p.UserId))
                .GroupBy(p => p.UserId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Points) })
                .ToDictionaryAsync(x => x.Key, x => x.Total);
        }

        var list = new List<LeaderboardRowDto>();
        for (var i = 0; i < top.Count; i++)
        {
            var entry = top[i];
            if (!users.TryGetValue(entry.UserId, out var user)) continue;
            var lifetime = allTimeTotals.GetValueOrDefault(entry.UserId, entry.Points);
            list.Add(new LeaderboardRowDto(
                Rank: i + 1,
                UserId: user.Id,
                Username: user.Username,
                Role: user.Role,
                AvatarUrl: user.AvatarUrl,
                Points: entry.Points,
                Level: ComputeLevel(lifetime),
                Title: ComputeTitle(lifetime)
            ));
        }

        LeaderboardMeDto? me = null;
        if (currentUserId.HasValue)
        {
            var uid = currentUserId.Value;
            var hasRank = rankLookup.TryGetValue(uid, out var info);
            var periodPoints = hasRank ? info.Points : 0;
            var totalPoints = period == "tum" ? periodPoints : await GetTotalPointsAsync(uid);
            var level = ComputeLevel(totalPoints);
            var (nextTitle, nextTitleRem) = ComputeNextTitle(totalPoints);

            me = new LeaderboardMeDto(
                Rank: hasRank ? info.Rank : null,
                Points: periodPoints,
                TotalPoints: totalPoints,
                Level: level,
                Title: ComputeTitle(totalPoints),
                LevelRequired: PointsForLevel(level),
                NextLevelRemaining: RemainingForNextLevel(totalPoints),
                NextTitle: nextTitle,
                NextTitleRemaining: nextTitleRem
            );
        }

        return new LeaderboardResponseDto(
            Period: period,
            UpdatedAt: DateTime.UtcNow,
            List: list,
            Me: me,
            Rules: GetRuleList(),
            DailyCap: DailyCap
        );
    }

    public async Task<LeaderboardMeDto> GetUserRankSnapshotAsync(uint userId)
    {
        var board = await GetLeaderboardAsync("tum", 1, userId);
        return board.Me ?? new LeaderboardMeDto(null, 0, 0, 1, Ranks[0].Title, 0, PointsForLevel(2), Ranks[1].Title, PointsForLevel(Ranks[1].Level));
    }

    private async Task<int> WriteRulePointsAsync(uint userId, string action, string reference)
    {
        var rule = Rules[action];
        if (rule.OncePerRef)
        {
            if (string.IsNullOrEmpty(reference) || reference.Length > 80) return 0;
            var already = await db.PointRecords.AsNoTracking()
                .AnyAsync(p => p.UserId == userId && p.Action == action && p.Reference == reference);
            if (already) return 0;
        }

        var dayStart = LocalDayStartUtc(LocalNow());
        if (rule.DailyLimit > 0)
        {
            var countToday = await db.PointRecords.AsNoTracking()
                .CountAsync(p => p.UserId == userId && p.Action == action && p.CreatedAt >= dayStart);
            if (countToday >= rule.DailyLimit) return 0;
        }

        var earnedToday = await db.PointRecords.AsNoTracking()
            .Where(p => p.UserId == userId && p.CreatedAt >= dayStart && p.Points > 0 && p.Action != ActionAdminBonus)
            .SumAsync(p => (int?)p.Points) ?? 0;

        var award = Math.Min(rule.Points, DailyCap - earnedToday);
        if (award <= 0) return 0;

        db.PointRecords.Add(new PointRecord
        {
            UserId = userId,
            Action = action,
            Points = award,
            Reference = reference,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return award;
    }
}
