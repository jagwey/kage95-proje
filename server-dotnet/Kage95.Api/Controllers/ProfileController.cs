using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Hesabım (profil bilgileri, istatistikler, avatar ve puan geçmişi) işlemleri.</summary>
[ApiController]
[Route("api/profile")]
[Tags("Profile (Hesabım)")]
public class ProfileController(Kage95DbContext db, PointsService points, IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    /// <summary>Giriş yapan kullanıcının Hesabım profil özetini, rütbesini ve son puan geçmişini döner.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<MyProfileDto>> GetMyProfile()
    {
        var current = HttpContext.RequireCurrentUser();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == current.Id)
            ?? throw new ApiException(404, "Kullanıcı bulunamadı.");

        var rank = await points.GetUserRankSnapshotAsync(user.Id);
        var stats = await LoadStatsAsync(user.Id);
        var recent = await db.PointRecords.AsNoTracking()
            .Where(p => p.UserId == user.Id && p.Points != 0)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(25)
            .ToListAsync();

        return Ok(new MyProfileDto(
            user.Id,
            user.Username,
            user.FirstName,
            user.LastName,
            user.Email,
            user.Role,
            user.Bio,
            user.AvatarUrl,
            user.CreatedAt,
            rank,
            stats,
            recent.Select(p => new PointHistoryItemDto(
                p.Id,
                p.Action,
                PointsService.ActionLabel(p.Action),
                p.Points,
                p.Note,
                p.CreatedAt
            )).ToList()
        ));
    }

    /// <summary>Ad, soyad, biyografi ve profil resmi URL bilgisini günceller.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<AuthResponse>> UpdateMyProfile([FromBody] UpdateProfileRequest input)
    {
        var current = HttpContext.RequireCurrentUser();
        var firstName = (input.FirstName ?? "").Trim();
        var lastName = (input.LastName ?? "").Trim();
        var bio = string.IsNullOrWhiteSpace(input.Bio) ? null : input.Bio.Trim();
        var avatarUrl = string.IsNullOrWhiteSpace(input.AvatarUrl) ? null : input.AvatarUrl.Trim();

        if (firstName.Length is < 1 or > 60 || lastName.Length is < 1 or > 60)
            throw new ApiException(400, "Ad ve soyad 1–60 karakter olmalı.");
        if (bio is { Length: > 300 })
            throw new ApiException(400, "Biyografi en fazla 300 karakter olabilir.");
        if (avatarUrl is { Length: > 500 })
            throw new ApiException(400, "Profil resmi adresi çok uzun.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id)
            ?? throw new ApiException(404, "Kullanıcı bulunamadı.");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Bio = bio;
        user.AvatarUrl = avatarUrl;
        await db.SaveChangesAsync();

        var earned = 0;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
            earned = await points.AwardAsync(user.Id, PointsService.ActionAvatar, "profil_resmi");

        return Ok(new AuthResponse(
            new UserDto(user.Id, user.Username, user.FirstName, user.LastName, user.Email, user.Role, user.Bio, user.AvatarUrl),
            earned
        ));
    }

    /// <summary>Kullanıcı profil resmi yükler (PNG/JPG/WEBP/GIF, en fazla 5 MB).</summary>
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResultDto>> UploadAvatar(IFormFile file)
    {
        var current = HttpContext.RequireCurrentUser();
        if (file is null || file.Length == 0)
            throw new ApiException(400, "Geçerli bir görsel dosyası seç.");
        if (file.Length > 5 * 1024 * 1024)
            throw new ApiException(400, "Dosya boyutu en fazla 5 MB olabilir.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ApiException(400, "Sadece PNG, JPG, WEBP veya GIF yükleyebilirsin.");

        var uploadsDir = Path.Combine(env.ContentRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"u{current.Id}-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsDir, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/avatars/{fileName}";
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == current.Id);
        if (user != null)
        {
            user.AvatarUrl = url;
            await db.SaveChangesAsync();
            await points.AwardAsync(user.Id, PointsService.ActionAvatar, "profil_resmi");
        }

        return Ok(new UploadResultDto(url));
    }

    /// <summary>Kullanıcı adına göre herkese açık profil özetini döner.</summary>
    [HttpGet("{username}")]
    public async Task<ActionResult<PublicProfileDto>> GetPublicProfile(string username)
    {
        var clean = (username ?? "").Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == clean)
            ?? throw new ApiException(404, "Kullanıcı bulunamadı.");

        var rank = await points.GetUserRankSnapshotAsync(user.Id);
        var stats = await LoadStatsAsync(user.Id);

        return Ok(new PublicProfileDto(
            user.Id,
            user.Username,
            user.Role,
            user.Bio,
            user.AvatarUrl,
            user.CreatedAt,
            rank.Rank,
            rank.TotalPoints,
            rank.Level,
            rank.Title,
            stats
        ));
    }

    private async Task<ProfileStatsDto> LoadStatsAsync(uint userId)
    {
        var favoritesCount = await db.Favorites.AsNoTracking().CountAsync(f => f.UserId == userId);
        var readingCount = await db.ReadingLists.AsNoTracking().CountAsync(r => r.UserId == userId && r.Status == ReadingStatuses.Reading);
        var completedCount = await db.ReadingLists.AsNoTracking().CountAsync(r => r.UserId == userId && r.Status == ReadingStatuses.Completed);
        var ratingsCount = await db.Ratings.AsNoTracking().CountAsync(r => r.UserId == userId);
        var commentsCount = await db.Comments.AsNoTracking().CountAsync(c => c.UserId == userId);
        var friendsCount = await db.Friendships.AsNoTracking().CountAsync(f =>
            f.Status == FriendshipStatuses.Accepted && (f.UserLow == userId || f.UserHigh == userId));

        return new ProfileStatsDto(favoritesCount, readingCount, completedCount, ratingsCount, commentsCount, friendsCount);
    }
}
