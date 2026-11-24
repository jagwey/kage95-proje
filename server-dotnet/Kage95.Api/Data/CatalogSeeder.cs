using System.Text.Json;
using Kage95.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Data;

public static class CatalogSeeder
{
    private sealed record SeedManga(
        uint Id,
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

    public static async Task SeedAsync(Kage95DbContext db, IConfiguration config, string contentRoot)
    {
        // 1. Seed initial mangas from shared/mangas.json if catalog is empty
        var seedPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "shared", "mangas.json"));
        if (File.Exists(seedPath) && !await db.Mangas.AnyAsync())
        {
            var json = await File.ReadAllTextAsync(seedPath);
            var items = JsonSerializer.Deserialize<List<SeedManga>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            foreach (var item in items)
            {
                db.Mangas.Add(new Manga
                {
                    Id = item.Id,
                    Title = item.Title,
                    JapaneseTitle = item.JapaneseTitle,
                    Author = item.Author,
                    Genres = item.Genres,
                    Summary = item.Summary,
                    Cover = item.Cover,
                    Status = item.Status,
                    CatalogRating = item.Rating,
                    Chapters = item.Chapters,
                    Year = item.Year,
                    Views = item.Views,
                    Accent = item.Accent,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await db.SaveChangesAsync();
        }

        // 2. Ensure configured owner/admin user (default: jagwey) always has admin role
        var adminUsername = (config["Kage95:SeedAdminUsername"] ?? "jagwey").Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(adminUsername))
        {
            var admin = await db.Users.FirstOrDefaultAsync(u => u.Username == adminUsername);
            if (admin != null && admin.Role != UserRoles.Admin)
            {
                admin.Role = UserRoles.Admin;
                await db.SaveChangesAsync();
            }
        }

        // 3. Ensure each existing manga has at least a sample Chapter 1 so the reader works out of the box
        var mangas = await db.Mangas.Include(m => m.ChapterItems).ToListAsync();
        var addedChapters = false;
        foreach (var manga in mangas)
        {
            if (manga.ChapterItems.Count == 0)
            {
                var chapter = new Chapter
                {
                    MangaId = manga.Id,
                    ChapterNumber = 1,
                    Title = $"{manga.Title} — 1. Bölüm (Önizleme)",
                    CreatedAt = DateTime.UtcNow,
                    Pages =
                    [
                        new ChapterPage { PageNumber = 1, ImageUrl = manga.Cover }
                    ]
                };
                db.Chapters.Add(chapter);
                addedChapters = true;
            }
        }
        if (addedChapters)
        {
            await db.SaveChangesAsync();
        }

        // 4. Backfill initial points from existing comments/ratings/favorites once if point_records is empty
        if (!await db.PointRecords.AnyAsync())
        {
            var now = DateTime.UtcNow;
            var users = await db.Users.AsNoTracking().ToListAsync();
            foreach (var user in users)
            {
                db.PointRecords.Add(new PointRecord
                {
                    UserId = user.Id,
                    Action = "gunluk_giris",
                    Points = 2,
                    Reference = $"gun:{user.CreatedAt:yyyy-MM-dd}",
                    CreatedAt = user.CreatedAt == default ? now : user.CreatedAt
                });
            }

            var favorites = await db.Favorites.AsNoTracking().ToListAsync();
            foreach (var fav in favorites)
            {
                db.PointRecords.Add(new PointRecord
                {
                    UserId = fav.UserId,
                    Action = "kutuphane_ekle",
                    Points = 3,
                    Reference = $"kutuphane:{fav.MangaId}",
                    CreatedAt = fav.CreatedAt == default ? now : fav.CreatedAt
                });
            }

            var ratings = await db.Ratings.AsNoTracking().ToListAsync();
            foreach (var rating in ratings)
            {
                db.PointRecords.Add(new PointRecord
                {
                    UserId = rating.UserId,
                    Action = "puan_ver",
                    Points = 2,
                    Reference = $"puan:{rating.MangaId}",
                    CreatedAt = now
                });
            }

            var comments = await db.Comments.AsNoTracking().ToListAsync();
            foreach (var comment in comments)
            {
                if (comment.Body.Trim().Length >= 20 && comment.Body.Trim().Distinct().Count() >= 6)
                {
                    db.PointRecords.Add(new PointRecord
                    {
                        UserId = comment.UserId,
                        Action = "yorum",
                        Points = 5,
                        Reference = $"yorum:{comment.Id}",
                        CreatedAt = comment.CreatedAt == default ? now : comment.CreatedAt
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
