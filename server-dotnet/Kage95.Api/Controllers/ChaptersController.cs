using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Manga bölümleri listeleme, sayfa okuma ve bölüm tamamlama işlemleri.</summary>
[ApiController]
[Route("api")]
[Tags("Chapters & Reader (Bölümler)")]
public class ChaptersController(Kage95DbContext db, PointsService points) : ControllerBase
{
    /// <summary>Bir manganın tüm bölümlerini bölüm numarasına göre sıralı döner.</summary>
    [HttpGet("mangas/{mangaId:int}/chapters")]
    public async Task<ActionResult<List<ChapterSummaryDto>>> GetChapters(uint mangaId)
    {
        if (!await db.Mangas.AsNoTracking().AnyAsync(m => m.Id == mangaId))
            throw new ApiException(404, "Manga bulunamadı.");

        var list = await db.Chapters.AsNoTracking()
            .Where(c => c.MangaId == mangaId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => new ChapterSummaryDto(
                c.Id,
                c.MangaId,
                c.ChapterNumber,
                c.Title,
                c.Pages.Count,
                c.CreatedAt
            ))
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>Bir bölümün sayfalarını ve önceki/sonraki bölüm bağlantılarını döner.</summary>
    [HttpGet("chapters/{chapterId:int}")]
    public async Task<ActionResult<ChapterDetailDto>> GetChapter(uint chapterId)
    {
        var chapter = await db.Chapters.AsNoTracking()
            .Include(c => c.Manga)
            .Include(c => c.Pages)
            .FirstOrDefaultAsync(c => c.Id == chapterId)
            ?? throw new ApiException(404, "Bölüm bulunamadı.");

        var siblings = await db.Chapters.AsNoTracking()
            .Where(c => c.MangaId == chapter.MangaId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => c.Id)
            .ToListAsync();

        var index = siblings.IndexOf(chapter.Id);
        uint? prevId = index > 0 ? siblings[index - 1] : null;
        uint? nextId = index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;

        var pages = chapter.Pages
            .OrderBy(p => p.PageNumber)
            .Select(p => new ChapterPageDto(p.Id, p.PageNumber, p.ImageUrl))
            .ToList();

        return Ok(new ChapterDetailDto(
            chapter.Id,
            chapter.MangaId,
            chapter.Manga.Title,
            chapter.ChapterNumber,
            chapter.Title,
            chapter.CreatedAt,
            pages,
            prevId,
            nextId
        ));
    }

    /// <summary>Bölümü okundu olarak işaretler, okuma ilerlemesini günceller ve +10 bölüm puanı kazandırır.</summary>
    [HttpPost("chapters/{chapterId:int}/complete")]
    [EnableRateLimiting("write")]
    public async Task<ActionResult<ActionResultDto>> CompleteChapter(uint chapterId)
    {
        var user = HttpContext.RequireCurrentUser();
        var chapter = await db.Chapters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == chapterId)
            ?? throw new ApiException(404, "Bölüm bulunamadı.");

        var chapterNum = (uint)Math.Max(1, Math.Floor(chapter.ChapterNumber));
        var entry = await db.ReadingLists.FirstOrDefaultAsync(r => r.UserId == user.Id && r.MangaId == chapter.MangaId);
        if (entry is null)
        {
            db.ReadingLists.Add(new ReadingListEntry
            {
                UserId = user.Id,
                MangaId = chapter.MangaId,
                Status = ReadingStatuses.Reading,
                CurrentChapter = chapterNum,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (chapterNum > entry.CurrentChapter)
        {
            entry.CurrentChapter = chapterNum;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var earned = await points.AwardAsync(user.Id, PointsService.ActionChapterRead, $"bolum:{chapter.Id}");
        return Ok(new ActionResultDto(earned));
    }
}
