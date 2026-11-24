using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kage95.Tests;

public class Kage95TestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"kage95-test-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<Kage95DbContext>>();
            services.RemoveAll<Kage95DbContext>();
            services.AddDbContext<Kage95DbContext>(opt => opt.UseInMemoryDatabase(_dbName));
        });
    }

    public async Task SeedMangaAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Kage95DbContext>();
        if (!await db.Mangas.AnyAsync())
        {
            var manga = new Manga
            {
                Id = 1,
                Title = "Wind Breaker",
                JapaneseTitle = "WIND BREAKER",
                Author = "Satoru Nii",
                Genres = ["Aksiyon", "Dram"],
                Summary = "Furin Lisesi hikayesi.",
                Cover = "covers/wind-breaker.png",
                Status = "Devam Ediyor",
                CatalogRating = 9.6m,
                Chapters = 214,
                Year = 2021,
                Views = "1.2M",
                Accent = "#68c58a",
                CreatedAt = DateTime.UtcNow
            };
            db.Mangas.Add(manga);
            db.Chapters.Add(new Chapter
            {
                Id = 1,
                MangaId = 1,
                ChapterNumber = 1,
                Title = "İlk Bölüm",
                CreatedAt = DateTime.UtcNow,
                Pages = [new ChapterPage { PageNumber = 1, ImageUrl = "covers/wind-breaker.png" }]
            });
            await db.SaveChangesAsync();
        }
    }

    public async Task MakeAdminAsync(string username)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Kage95DbContext>();
        var user = await db.Users.FirstAsync(u => u.Username == username);
        user.Role = UserRoles.Admin;
        await db.SaveChangesAsync();
    }
}

public class ApiIntegrationTests
{
    private static HttpRequestMessage WriteReq(HttpMethod method, string url, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Kage95", "1");
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    [Fact]
    public async Task Swagger_And_CsrfProtection_Work()
    {
        await using var factory = new Kage95TestFactory();
        await factory.SeedMangaAsync();
        var client = factory.CreateClient();

        var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        var json = await swagger.Content.ReadAsStringAsync();
        Assert.Contains("/api/leaderboard", json);
        Assert.Contains("/api/admin/users/{id}/role", json);
        Assert.Contains("/api/reading-list/{mangaId}", json);

        // Missing X-Kage95 header on write must be 403
        var noHeader = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
    }

    [Fact]
    public async Task FullFlow_Auth_Profile_Leaderboard_ReadingList_Chapters_Social_And_AdminRoles()
    {
        await using var factory = new Kage95TestFactory();
        await factory.SeedMangaAsync();

        var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        var userClient = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // 1. Register admin (jagwey) and second user (mod_aday)
        var r1 = await adminClient.SendAsync(WriteReq(HttpMethod.Post, "/api/auth/register",
            new RegisterRequest("Ali", "Sonmezgul", "jagwey@test.local", "jagwey", "SuperSecret123")));
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        await factory.MakeAdminAsync("jagwey");

        var r2 = await userClient.SendAsync(WriteReq(HttpMethod.Post, "/api/auth/register",
            new RegisterRequest("Test", "Mod", "mod@test.local", "mod_aday", "SuperSecret123")));
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var modUser = (await r2.Content.ReadFromJsonAsync<AuthResponse>())!.User;

        // 2. Profile (Hesabım) update + avatar points
        var profPut = await adminClient.SendAsync(WriteReq(HttpMethod.Put, "/api/profile/me",
            new UpdateProfileRequest("Ali Osman", "Sonmezgul", "KAGE95 kurucusu", "/uploads/avatars/a.png")));
        Assert.Equal(HttpStatusCode.OK, profPut.StatusCode);

        var myProf = await adminClient.GetFromJsonAsync<MyProfileDto>("/api/profile/me");
        Assert.NotNull(myProf);
        Assert.Equal("admin", myProf!.Role);
        Assert.Equal("KAGE95 kurucusu", myProf.Bio);
        Assert.True(myProf.Rank.TotalPoints >= 12); // daily login + avatar

        // 3. Reading list + complete chapter (+10 points)
        var rlPut = await adminClient.SendAsync(WriteReq(HttpMethod.Put, "/api/reading-list/1",
            new UpsertReadingListRequest("reading", 1)));
        Assert.Equal(HttpStatusCode.OK, rlPut.StatusCode);

        var complete = await adminClient.SendAsync(WriteReq(HttpMethod.Post, "/api/chapters/1/complete"));
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        var completeBody = await complete.Content.ReadFromJsonAsync<ActionResultDto>();
        Assert.Equal(10, completeBody!.PointsEarned);

        // 4. Leaderboard reflects points
        var lb = await adminClient.GetFromJsonAsync<LeaderboardResponseDto>("/api/leaderboard?period=tum");
        Assert.NotNull(lb);
        Assert.Contains(lb!.List, row => row.Username == "jagwey" && row.Points >= 25);

        // 5. Admin grants moderator role to mod_aday, and normal user before promotion is blocked
        var modBefore = await userClient.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.Forbidden, modBefore.StatusCode);

        var rolePatch = await adminClient.SendAsync(WriteReq(HttpMethod.Patch, $"/api/admin/users/{modUser.Id}/role",
            new UpdateUserRoleRequest("moderator")));
        Assert.Equal(HttpStatusCode.OK, rolePatch.StatusCode);

        // Now mod_aday can view stats and create a chapter, but CANNOT change roles
        var modAfter = await userClient.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, modAfter.StatusCode);

        var modCreateChapter = await userClient.SendAsync(WriteReq(HttpMethod.Post, "/api/admin/mangas/1/chapters",
            new CreateChapterRequest(2, "İkinci Bölüm", ["covers/wind-breaker.png"])));
        Assert.Equal(HttpStatusCode.Created, modCreateChapter.StatusCode);

        var modTryRole = await userClient.SendAsync(WriteReq(HttpMethod.Patch, $"/api/admin/users/{modUser.Id}/role",
            new UpdateUserRoleRequest("admin")));
        Assert.Equal(HttpStatusCode.Forbidden, modTryRole.StatusCode);

        // 6. Admin grants bonus points
        var bonus = await adminClient.SendAsync(WriteReq(HttpMethod.Post, $"/api/admin/users/{modUser.Id}/points",
            new AdjustUserPointsRequest(300, "Mod katkısı")));
        Assert.Equal(HttpStatusCode.OK, bonus.StatusCode);
        var bonusJson = await bonus.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Genç Gölge", bonusJson.GetProperty("title").GetString());

        // 7. Social: friend request -> accept -> unfriend -> block -> search hidden -> unblock
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(WriteReq(HttpMethod.Post, $"/api/social/friends/{modUser.Id}"))).StatusCode);
        var adminInfo = (await adminClient.GetFromJsonAsync<AuthMeResponse>("/api/auth/me"))!.User!;
        Assert.Equal(HttpStatusCode.NoContent, (await userClient.SendAsync(WriteReq(HttpMethod.Patch, $"/api/social/friends/{adminInfo.Id}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(WriteReq(HttpMethod.Delete, $"/api/social/friends/{modUser.Id}"))).StatusCode);

        var friendsAfterRemove = await adminClient.GetFromJsonAsync<List<FriendDto>>("/api/social/friends");
        Assert.Empty(friendsAfterRemove!);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(WriteReq(HttpMethod.Post, $"/api/social/blocks/{modUser.Id}"))).StatusCode);
        var searchBlocked = await userClient.GetFromJsonAsync<List<ProfileSummaryDto>>("/api/social/users?username=jagwey");
        Assert.Empty(searchBlocked!);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.SendAsync(WriteReq(HttpMethod.Delete, $"/api/social/blocks/{modUser.Id}"))).StatusCode);
    }
}
