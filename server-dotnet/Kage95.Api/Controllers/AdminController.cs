using System.Text.RegularExpressions;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Admin (tam yetki) ve Moderatör içerik/kullanıcı/rol yönetim paneli uçları.</summary>
[ApiController]
[Route("api/admin")]
[Tags("Admin & Moderation Panel")]
public partial class AdminController(Kage95DbContext db, PointsService points, IWebHostEnvironment env) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif"
    };

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorRegex();

    /// <summary>Genel platform istatistiklerini döner (Admin veya Moderatör).</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetStats()
    {
        HttpContext.RequireStaff();
        return Ok(new AdminStatsDto(
            Users: await db.Users.AsNoTracking().CountAsync(),
            Mangas: await db.Mangas.AsNoTracking().CountAsync(),
            Chapters: await db.Chapters.AsNoTracking().CountAsync(),
            Comments: await db.Comments.AsNoTracking().CountAsync(),
            Ratings: await db.Ratings.AsNoTracking().CountAsync(),
            Messages: await db.Messages.AsNoTracking().CountAsync()
        ));
    }

    // ==================== USER & ROLE MANAGEMENT (ADMIN ONLY — TAM YETKİ) ====================

    /// <summary>Tüm kullanıcıları rol ve puan bilgileriyle listeler (Sadece Admin).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<AdminUserDto>>> GetUsers()
    {
        HttpContext.RequireAdmin();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync();
        var totals = await db.PointRecords.AsNoTracking()
            .Where(p => p.Points != 0)
            .GroupBy(p => p.UserId)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Points) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

        var result = users.Select(u =>
        {
            var total = totals.GetValueOrDefault(u.Id, 0);
            return new AdminUserDto(
                u.Id,
                u.Username,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Role,
                total,
                PointsService.ComputeLevel(total),
                PointsService.ComputeTitle(total),
                u.CreatedAt
            );
        }).ToList();

        return Ok(result);
    }

    /// <summary>Bir kullanıcının rolünü değiştirir: user | moderator | admin (Sadece Admin).</summary>
    [HttpPatch("users/{id:int}/role")]
    public async Task<ActionResult<AdminUserDto>> UpdateUserRole(uint id, [FromBody] UpdateUserRoleRequest input)
    {
        var admin = HttpContext.RequireAdmin();
        var newRole = (input.Role ?? "").Trim().ToLowerInvariant();
        if (!UserRoles.All.Contains(newRole))
            throw new ApiException(400, "Geçerli roller: user, moderator, admin.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new ApiException(404, "Kullanıcı bulunamadı.");

        if (target.Id == admin.Id && newRole != UserRoles.Admin)
            throw new ApiException(400, "Kendi admin yetkini kaldıramazsın.");

        target.Role = newRole;
        await db.SaveChangesAsync();

        var total = await points.GetTotalPointsAsync(target.Id);
        return Ok(new AdminUserDto(
            target.Id, target.Username, target.FirstName, target.LastName, target.Email, target.Role,
            total, PointsService.ComputeLevel(total), PointsService.ComputeTitle(total), target.CreatedAt
        ));
    }

    /// <summary>Bir kullanıcıya bonus puan ekler veya ceza puanı düşer (Sadece Admin).</summary>
    [HttpPost("users/{id:int}/points")]
    public async Task<ActionResult<object>> AdjustUserPoints(uint id, [FromBody] AdjustUserPointsRequest input)
    {
        HttpContext.RequireAdmin();
        if (input.Points == 0 || Math.Abs(input.Points) > 100000)
            throw new ApiException(400, "Puan değişimi -100000 ile +100000 arasında ve sıfırdan farklı olmalı.");

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id))
            throw new ApiException(404, "Kullanıcı bulunamadı.");

        var newTotal = await points.AdjustByAdminAsync(id, input.Points, input.Note);
        return Ok(new
        {
            userId = id,
            totalPoints = newTotal,
            level = PointsService.ComputeLevel(newTotal),
            title = PointsService.ComputeTitle(newTotal)
        });
    }

    /// <summary>Bir kullanıcı hesabını siler (Sadece Admin, kendini silemez).</summary>
    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(uint id)
    {
        var admin = HttpContext.RequireAdmin();
        if (id == admin.Id)
            throw new ApiException(400, "Kendi hesabını buradan silemezsin.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == id)
            ?? throw new ApiException(404, "Kullanıcı bulunamadı.");

        db.Users.Remove(target);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ==================== MANGA & CHAPTER MANAGEMENT (ADMIN OR MODERATOR) ====================

    /// <summary>Yeni manga ekler (Admin veya Moderatör).</summary>
    [HttpPost("mangas")]
    public async Task<ActionResult<MangaDto>> CreateManga([FromBody] UpsertMangaRequest input)
    {
        HttpContext.RequireStaff();
        ValidateMangaInput(input);

        var manga = new Manga
        {
            Title = input.Title.Trim(),
            JapaneseTitle = string.IsNullOrWhiteSpace(input.JapaneseTitle) ? input.Title.Trim() : input.JapaneseTitle.Trim(),
            Author = input.Author.Trim(),
            Genres = input.Genres.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct().ToList(),
            Summary = input.Summary.Trim(),
            Cover = input.Cover.Trim(),
            Status = input.Status.Trim(),
            CatalogRating = Math.Clamp(Math.Round(input.Rating, 1), 1.0m, 10.0m),
            Chapters = input.Chapters,
            Year = input.Year,
            Views = string.IsNullOrWhiteSpace(input.Views) ? "0" : input.Views.Trim(),
            Accent = HexColorRegex().IsMatch(input.Accent ?? "") ? input.Accent!.Trim() : "#68c58a",
            CreatedAt = DateTime.UtcNow
        };
        db.Mangas.Add(manga);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new MangaDto(
            manga.Id, manga.Title, manga.JapaneseTitle, manga.Author, manga.Genres, manga.Summary,
            manga.Cover, manga.Status, manga.CatalogRating, 0, manga.Chapters, 0, manga.Year, manga.Views, manga.Accent
        ));
    }

    /// <summary>Mevcut bir mangayı günceller (Admin veya Moderatör).</summary>
    [HttpPut("mangas/{id:int}")]
    public async Task<ActionResult<MangaDto>> UpdateManga(uint id, [FromBody] UpsertMangaRequest input)
    {
        HttpContext.RequireStaff();
        ValidateMangaInput(input);

        var manga = await db.Mangas.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ApiException(404, "Manga bulunamadı.");

        manga.Title = input.Title.Trim();
        manga.JapaneseTitle = string.IsNullOrWhiteSpace(input.JapaneseTitle) ? manga.Title : input.JapaneseTitle.Trim();
        manga.Author = input.Author.Trim();
        manga.Genres = input.Genres.Select(g => g.Trim()).Where(g => g.Length > 0).Distinct().ToList();
        manga.Summary = input.Summary.Trim();
        manga.Cover = input.Cover.Trim();
        manga.Status = input.Status.Trim();
        manga.CatalogRating = Math.Clamp(Math.Round(input.Rating, 1), 1.0m, 10.0m);
        manga.Chapters = input.Chapters;
        manga.Year = input.Year;
        manga.Views = string.IsNullOrWhiteSpace(input.Views) ? manga.Views : input.Views.Trim();
        if (HexColorRegex().IsMatch(input.Accent ?? "")) manga.Accent = input.Accent!.Trim();

        await db.SaveChangesAsync();

        var uploadedChapters = await db.Chapters.AsNoTracking().CountAsync(c => c.MangaId == manga.Id);
        return Ok(new MangaDto(
            manga.Id, manga.Title, manga.JapaneseTitle, manga.Author, manga.Genres, manga.Summary,
            manga.Cover, manga.Status, manga.CatalogRating, 0, manga.Chapters, uploadedChapters, manga.Year, manga.Views, manga.Accent
        ));
    }

    /// <summary>Bir mangayı ve bağlı bölümlerini/yorumlarını siler (Admin veya Moderatör).</summary>
    [HttpDelete("mangas/{id:int}")]
    public async Task<IActionResult> DeleteManga(uint id)
    {
        HttpContext.RequireStaff();
        var manga = await db.Mangas.FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new ApiException(404, "Manga bulunamadı.");

        db.Mangas.Remove(manga);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Bir mangaya yeni bölüm ve sıralı sayfa görselleri ekler (Admin veya Moderatör).</summary>
    [HttpPost("mangas/{mangaId:int}/chapters")]
    public async Task<ActionResult<ChapterSummaryDto>> CreateChapter(uint mangaId, [FromBody] CreateChapterRequest input)
    {
        HttpContext.RequireStaff();
        var manga = await db.Mangas.FirstOrDefaultAsync(m => m.Id == mangaId)
            ?? throw new ApiException(404, "Manga bulunamadı.");

        if (input.ChapterNumber <= 0)
            throw new ApiException(400, "Bölüm numarası 0'dan büyük olmalı.");
        var title = string.IsNullOrWhiteSpace(input.Title) ? $"{input.ChapterNumber}. Bölüm" : input.Title.Trim();
        if (title.Length > 150)
            throw new ApiException(400, "Bölüm başlığı en fazla 150 karakter olabilir.");

        var urls = (input.PageUrls ?? []).Select(u => u.Trim()).Where(u => u.Length > 0).ToList();
        if (urls.Count == 0)
            throw new ApiException(400, "En az 1 sayfa görseli adresi ekle.");

        var duplicate = await db.Chapters.AsNoTracking()
            .AnyAsync(c => c.MangaId == mangaId && c.ChapterNumber == input.ChapterNumber);
        if (duplicate)
            throw new ApiException(409, "Bu bölüm numarası zaten mevcut.");

        var chapter = new Chapter
        {
            MangaId = mangaId,
            ChapterNumber = input.ChapterNumber,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            Pages = urls.Select((url, idx) => new ChapterPage
            {
                PageNumber = (uint)(idx + 1),
                ImageUrl = url
            }).ToList()
        };

        db.Chapters.Add(chapter);
        if (input.ChapterNumber > manga.Chapters)
            manga.Chapters = (uint)Math.Ceiling(input.ChapterNumber);

        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new ChapterSummaryDto(chapter.Id, chapter.MangaId, chapter.ChapterNumber, chapter.Title, chapter.Pages.Count, chapter.CreatedAt));
    }

    /// <summary>Bir bölümü ve sayfalarını siler (Admin veya Moderatör).</summary>
    [HttpDelete("chapters/{chapterId:int}")]
    public async Task<IActionResult> DeleteChapter(uint chapterId)
    {
        HttpContext.RequireStaff();
        var chapter = await db.Chapters.FirstOrDefaultAsync(c => c.Id == chapterId)
            ?? throw new ApiException(404, "Bölüm bulunamadı.");

        db.Chapters.Remove(chapter);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Herhangi bir yorumu moderasyon kapsamında siler (Admin veya Moderatör).</summary>
    [HttpDelete("comments/{commentId:int}")]
    public async Task<IActionResult> DeleteCommentAsStaff(uint commentId)
    {
        HttpContext.RequireStaff();
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId)
            ?? throw new ApiException(404, "Yorum bulunamadı.");

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        await points.RemoveCommentPointsAsync(commentId);
        return NoContent();
    }

    /// <summary>Kapak veya bölüm sayfası görseli yükler (PNG/JPG/WEBP/GIF, en fazla 10 MB).</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<UploadResultDto>> UploadImage(IFormFile file)
    {
        HttpContext.RequireStaff();
        if (file is null || file.Length == 0)
            throw new ApiException(400, "Geçerli bir görsel dosyası seç.");
        if (file.Length > 10 * 1024 * 1024)
            throw new ApiException(400, "Dosya boyutu en fazla 10 MB olabilir.");

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            throw new ApiException(400, "Sadece PNG, JPG, WEBP veya GIF yükleyebilirsin.");

        var uploadsDir = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new UploadResultDto($"/uploads/{fileName}"));
    }

    private static void ValidateMangaInput(UpsertMangaRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 150)
            throw new ApiException(400, "Başlık 1–150 karakter olmalı.");
        if (string.IsNullOrWhiteSpace(input.Author) || input.Author.Trim().Length > 150)
            throw new ApiException(400, "Yazar 1–150 karakter olmalı.");
        if (input.Genres is null || input.Genres.Count == 0)
            throw new ApiException(400, "En az bir tür gir.");
        if (string.IsNullOrWhiteSpace(input.Summary))
            throw new ApiException(400, "Özet boş olamaz.");
        if (string.IsNullOrWhiteSpace(input.Cover) || input.Cover.Trim().Length > 255)
            throw new ApiException(400, "Kapak yolu 1–255 karakter olmalı.");
        if (string.IsNullOrWhiteSpace(input.Status) || input.Status.Trim().Length > 30)
            throw new ApiException(400, "Durum bilgisi geçersiz.");
        if (input.Year is < 1900 or > 2100)
            throw new ApiException(400, "Yıl 1900–2100 arasında olmalı.");
    }
}
