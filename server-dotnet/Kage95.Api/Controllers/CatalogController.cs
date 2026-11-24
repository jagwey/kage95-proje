using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Manga kataloğu, favoriler, 1–10 puanlama ve yorum işlemleri.</summary>
[ApiController]
[Route("api")]
[Tags("Catalog & Discussion")]
public class CatalogController(Kage95DbContext db, PointsService points) : ControllerBase
{
    /// <summary>Tüm manga kataloğunu gerçek puan ortalaması ve yüklenen bölüm sayısıyla döner.</summary>
    [HttpGet("mangas")]
    public async Task<ActionResult<List<MangaDto>>> GetMangas()
    {
        var mangas = await db.Mangas.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        var ratingStats = await db.Ratings.AsNoTracking()
            .GroupBy(r => r.MangaId)
            .Select(g => new { MangaId = g.Key, Avg = g.Average(x => (double)x.Score), Count = g.Count() })
            .ToDictionaryAsync(x => x.MangaId);
        var chapterCounts = await db.Chapters.AsNoTracking()
            .GroupBy(c => c.MangaId)
            .Select(g => new { MangaId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MangaId, x => x.Count);

        var result = mangas.Select(m =>
        {
            var hasRatings = ratingStats.TryGetValue(m.Id, out var stat);
            var rating = hasRatings ? Math.Round((decimal)stat!.Avg, 1, MidpointRounding.AwayFromZero) : m.CatalogRating;
            var ratingCount = hasRatings ? stat!.Count : 0;
            return new MangaDto(
                m.Id,
                m.Title,
                m.JapaneseTitle,
                m.Author,
                m.Genres,
                m.Summary,
                m.Cover,
                m.Status,
                rating,
                ratingCount,
                m.Chapters,
                chapterCounts.GetValueOrDefault(m.Id, 0),
                m.Year,
                m.Views,
                m.Accent
            );
        }).ToList();

        return Ok(result);
    }

    /// <summary>Giriş yapan kullanıcının favori manga ID listesini döner.</summary>
    [HttpGet("favorites")]
    public async Task<ActionResult<List<uint>>> GetFavorites()
    {
        var user = HttpContext.RequireCurrentUser();
        return Ok(await GetFavoriteIdsAsync(user.Id));
    }

    /// <summary>Misafir favorilerini hesaba toplu olarak aktarır (1–300 adet).</summary>
    [HttpPut("favorites")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<List<uint>>> MergeFavorites([FromBody] FavoritesBatchRequest input)
    {
        var user = HttpContext.RequireCurrentUser();
        if (input.Ids is null || input.Ids.Count == 0 || input.Ids.Count > 300)
            throw new ApiException(400, "1–300 favori gönder.");

        var distinctIds = input.Ids.Where(x => x > 0).Distinct().ToList();
        var validMangaIds = await db.Mangas.AsNoTracking()
            .Where(m => distinctIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync();
        var existingIds = await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == user.Id && validMangaIds.Contains(f.MangaId))
            .Select(f => f.MangaId)
            .ToHashSetAsync();

        var toAdd = validMangaIds.Where(id => !existingIds.Contains(id)).ToList();
        foreach (var mangaId in toAdd)
        {
            db.Favorites.Add(new Favorite { UserId = user.Id, MangaId = mangaId, CreatedAt = DateTime.UtcNow });
        }
        if (toAdd.Count > 0)
        {
            await db.SaveChangesAsync();
            foreach (var mangaId in toAdd)
                await points.AwardAsync(user.Id, PointsService.ActionLibraryAdd, $"kutuphane:{mangaId}");
        }

        return Ok(await GetFavoriteIdsAsync(user.Id));
    }

    /// <summary>Bir mangayı favorilere ekler.</summary>
    [HttpPut("favorites/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ActionResultDto>> AddFavorite(uint id)
    {
        var user = HttpContext.RequireCurrentUser();
        await RequireMangaExistsAsync(id);

        var exists = await db.Favorites.AsNoTracking().AnyAsync(f => f.UserId == user.Id && f.MangaId == id);
        if (!exists)
        {
            db.Favorites.Add(new Favorite { UserId = user.Id, MangaId = id, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var earned = await points.AwardAsync(user.Id, PointsService.ActionLibraryAdd, $"kutuphane:{id}");
        return Ok(new ActionResultDto(earned));
    }

    /// <summary>Bir mangayı favorilerden çıkarır.</summary>
    [HttpDelete("favorites/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> RemoveFavorite(uint id)
    {
        var user = HttpContext.RequireCurrentUser();
        var fav = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == user.Id && f.MangaId == id);
        if (fav != null)
        {
            db.Favorites.Remove(fav);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    /// <summary>Manganın puan özetini ve sayfalanmış yorumlarını döner.</summary>
    [HttpGet("mangas/{id:int}/discussion")]
    public async Task<ActionResult<DiscussionDto>> GetDiscussion(uint id, [FromQuery] uint? before = null)
    {
        await RequireMangaExistsAsync(id);
        var user = HttpContext.GetCurrentUser();

        var ratings = await db.Ratings.AsNoTracking().Where(r => r.MangaId == id).ToListAsync();
        decimal? avg = ratings.Count > 0
            ? Math.Round((decimal)ratings.Average(r => (double)r.Score), 1, MidpointRounding.AwayFromZero)
            : null;
        int? mine = user != null ? ratings.FirstOrDefault(r => r.UserId == user.Id)?.Score : null;

        var commentQuery = db.Comments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.MangaId == id);
        if (before.HasValue)
            commentQuery = commentQuery.Where(c => c.Id < before.Value);

        var comments = await commentQuery
            .OrderByDescending(c => c.Id)
            .Take(31)
            .Select(c => new CommentDto(c.Id, c.UserId, c.User.Username, c.User.Role, c.Body, c.CreatedAt))
            .ToListAsync();

        return Ok(new DiscussionDto(
            new RatingSummaryDto(avg, ratings.Count, mine),
            comments.Take(30).ToList(),
            comments.Count > 30
        ));
    }

    /// <summary>Bir mangaya 1–10 arası puan verir veya mevcut puanı günceller.</summary>
    [HttpPut("mangas/{id:int}/rating")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ActionResultDto>> UpsertRating(uint id, [FromBody] RatingRequest input)
    {
        var user = HttpContext.RequireCurrentUser();
        if (input.Score is < 1 or > 10)
            throw new ApiException(400, "Puan 1 ile 10 arasında olmalı.");
        await RequireMangaExistsAsync(id);

        var existing = await db.Ratings.FirstOrDefaultAsync(r => r.MangaId == id && r.UserId == user.Id);
        if (existing is null)
            db.Ratings.Add(new Rating { MangaId = id, UserId = user.Id, Score = (byte)input.Score });
        else
            existing.Score = (byte)input.Score;

        await db.SaveChangesAsync();
        var earned = await points.AwardAsync(user.Id, PointsService.ActionRate, $"puan:{id}");
        return Ok(new ActionResultDto(earned));
    }

    /// <summary>Bir mangaya yeni yorum ekler.</summary>
    [HttpPost("mangas/{id:int}/comments")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ActionResultDto>> CreateComment(uint id, [FromBody] CommentRequest input)
    {
        var user = HttpContext.RequireCurrentUser();
        var body = (input.Body ?? "").Trim();
        if (body.Length is < 1 or > 1000)
            throw new ApiException(400, "Yorum 1–1000 karakter olmalı.");
        await RequireMangaExistsAsync(id);

        var comment = new Comment
        {
            MangaId = id,
            UserId = user.Id,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var earned = await points.AwardCommentAsync(user.Id, comment.Id, body);
        return StatusCode(StatusCodes.Status201Created, new ActionResultDto(earned));
    }

    /// <summary>Yorum sahibinin veya moderatör/admin’in yorumu silmesini sağlar.</summary>
    [HttpDelete("comments/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> DeleteComment(uint id)
    {
        var user = HttpContext.RequireCurrentUser();
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new ApiException(404, "Yorum bulunamadı.");

        if (comment.UserId != user.Id && !UserRoles.IsStaff(user.Role))
            throw new ApiException(403, "Bu yorumu silme yetkin yok.");

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        await points.RemoveCommentPointsAsync(id);
        return NoContent();
    }

    private async Task<List<uint>> GetFavoriteIdsAsync(uint userId)
        => await db.Favorites.AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.MangaId)
            .ToListAsync();

    private async Task RequireMangaExistsAsync(uint id)
    {
        if (id == 0 || !await db.Mangas.AsNoTracking().AnyAsync(m => m.Id == id))
            throw new ApiException(404, "Manga bulunamadı.");
    }
}
