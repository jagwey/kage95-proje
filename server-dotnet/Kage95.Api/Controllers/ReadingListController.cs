using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Okuma listesi ve bölüm ilerleme takibi işlemleri.</summary>
[ApiController]
[Route("api/reading-list")]
[Tags("Reading List (Okuma Listem)")]
public class ReadingListController(Kage95DbContext db, PointsService points) : ControllerBase
{
    /// <summary>Giriş yapan kullanıcının tüm okuma listesini döner.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReadingListItemDto>>> GetMyList()
    {
        var user = HttpContext.RequireCurrentUser();
        var items = await db.ReadingLists.AsNoTracking()
            .Include(r => r.Manga)
            .Where(r => r.UserId == user.Id)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ReadingListItemDto(
                r.MangaId,
                r.Manga.Title,
                r.Manga.Cover,
                r.Manga.Chapters,
                r.Status,
                r.CurrentChapter,
                r.UpdatedAt
            ))
            .ToListAsync();

        return Ok(items);
    }

    /// <summary>Bir manga için okuma durumunu (reading/completed/plan_to_read/dropped) ve kalınan bölümü kaydeder.</summary>
    [HttpPut("{mangaId:int}")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ActionResultDto>> Upsert(uint mangaId, [FromBody] UpsertReadingListRequest input)
    {
        var user = HttpContext.RequireCurrentUser();
        var status = (input.Status ?? "").Trim().ToLowerInvariant();
        if (!ReadingStatuses.All.Contains(status))
            throw new ApiException(400, "Geçersiz okuma durumu. Geçerli değerler: reading, completed, plan_to_read, dropped.");

        var manga = await db.Mangas.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mangaId)
            ?? throw new ApiException(404, "Manga bulunamadı.");

        var chapter = input.CurrentChapter;
        if (status == ReadingStatuses.Completed && chapter == 0 && manga.Chapters > 0)
            chapter = manga.Chapters;

        var existing = await db.ReadingLists.FirstOrDefaultAsync(r => r.UserId == user.Id && r.MangaId == mangaId);
        if (existing is null)
        {
            db.ReadingLists.Add(new ReadingListEntry
            {
                UserId = user.Id,
                MangaId = mangaId,
                Status = status,
                CurrentChapter = chapter,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Status = status;
            existing.CurrentChapter = chapter;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        var earned = await points.AwardAsync(user.Id, PointsService.ActionLibraryAdd, $"kutuphane:{mangaId}");
        return Ok(new ActionResultDto(earned));
    }

    /// <summary>Bir mangayı okuma listesinden kaldırır.</summary>
    [HttpDelete("{mangaId:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(uint mangaId)
    {
        var user = HttpContext.RequireCurrentUser();
        var existing = await db.ReadingLists.FirstOrDefaultAsync(r => r.UserId == user.Id && r.MangaId == mangaId);
        if (existing != null)
        {
            db.ReadingLists.Remove(existing);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
