using System.Text.RegularExpressions;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Kullanıcı arama, arkadaşlık istekleri, arkadaşlıktan çıkarma, engelleme ve sohbet işlemleri.</summary>
[ApiController]
[Route("api/social")]
[Tags("Social (Arkadaşlar, Engelleme & Sohbet)")]
public partial class SocialController(Kage95DbContext db) : ControllerBase
{
    [GeneratedRegex(@"^[a-z0-9_]{3,24}$")]
    private static partial Regex UsernameRegex();

    private static (uint Low, uint High) Pair(uint me, uint other)
    {
        if (other == 0) throw new ApiException(400, "Geçersiz kayıt numarası.");
        if (me == other) throw new ApiException(400, "Kendi hesabınla bu işlemi yapamazsın.");
        return me < other ? (me, other) : (other, me);
    }

    /// <summary>Kullanıcı adıyla kullanıcı arar (engelli ilişkileri gizler).</summary>
    [HttpGet("users")]
    public async Task<ActionResult<List<ProfileSummaryDto>>> SearchUsers([FromQuery] string? username)
    {
        var me = HttpContext.RequireCurrentUser();
        var clean = (username ?? "").Trim().ToLowerInvariant();
        if (!UsernameRegex().IsMatch(clean)) return Ok(new List<ProfileSummaryDto>());

        var target = await db.Users.AsNoTracking()
            .Where(u => u.Username == clean && u.Id != me.Id)
            .Select(u => new ProfileSummaryDto(u.Id, u.Username, u.Role))
            .FirstOrDefaultAsync();
        if (target is null) return Ok(new List<ProfileSummaryDto>());

        var (low, high) = Pair(me.Id, target.Id);
        var blocked = await db.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserLow == low && f.UserHigh == high && f.Status == FriendshipStatuses.Blocked);
        if (blocked) return Ok(new List<ProfileSummaryDto>());

        return Ok(new List<ProfileSummaryDto> { target });
    }

    /// <summary>Aktif arkadaşları ve bekleyen istekleri döner (engellenenler hariç).</summary>
    [HttpGet("friends")]
    public async Task<ActionResult<List<FriendDto>>> GetFriends()
    {
        var me = HttpContext.RequireCurrentUser();
        var rows = await db.Friendships.AsNoTracking()
            .Include(f => f.LowUser)
            .Include(f => f.HighUser)
            .Where(f => (f.UserLow == me.Id || f.UserHigh == me.Id) && f.Status != FriendshipStatuses.Blocked)
            .ToListAsync();

        var list = rows.Select(f =>
        {
            var other = f.UserLow == me.Id ? f.HighUser : f.LowUser;
            return new FriendDto(other.Id, other.Username, other.Role, f.Status, f.RequestedBy);
        }).OrderBy(x => x.Username).ToList();

        return Ok(list);
    }

    /// <summary>Bir kullanıcıya arkadaşlık isteği gönderir.</summary>
    [HttpPost("friends/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> SendFriendRequest(uint id)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id))
            throw new ApiException(404, "Kullanıcı bulunamadı.");

        var existing = await db.Friendships.FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (existing is null)
        {
            db.Friendships.Add(new Friendship
            {
                UserLow = low,
                UserHigh = high,
                RequestedBy = me.Id,
                Status = FriendshipStatuses.Pending
            });
            await db.SaveChangesAsync();
        }
        else if (existing.Status == FriendshipStatuses.Blocked)
        {
            throw new ApiException(403, "Engellenmiş bir kullanıcıya arkadaşlık isteği gönderemezsin.");
        }

        return NoContent();
    }

    /// <summary>Gelen arkadaşlık isteğini kabul eder.</summary>
    [HttpPatch("friends/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> AcceptFriendRequest(uint id)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        var existing = await db.Friendships.FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (existing?.Status == FriendshipStatuses.Accepted) return NoContent();
        if (existing is null || existing.Status != FriendshipStatuses.Pending || existing.RequestedBy == me.Id)
            throw new ApiException(409, "Kabul edilebilecek bir istek bulunamadı.");

        existing.Status = FriendshipStatuses.Accepted;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Bekleyen isteği iptal/reddeder VEYA kabul edilmiş arkadaşı listeden çıkarır.</summary>
    [HttpDelete("friends/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> RemoveFriendOrRequest(uint id)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        var existing = await db.Friendships.FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (existing != null && existing.Status != FriendshipStatuses.Blocked)
        {
            db.Friendships.Remove(existing);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    /// <summary>Kullanıcının engellediği hesapları listeler.</summary>
    [HttpGet("blocks")]
    public async Task<ActionResult<List<BlockedUserDto>>> GetBlockedUsers()
    {
        var me = HttpContext.RequireCurrentUser();
        var rows = await db.Friendships.AsNoTracking()
            .Include(f => f.LowUser)
            .Include(f => f.HighUser)
            .Where(f => f.Status == FriendshipStatuses.Blocked && f.BlockedBy == me.Id)
            .ToListAsync();

        var list = rows.Select(f =>
        {
            var other = f.UserLow == me.Id ? f.HighUser : f.LowUser;
            return new BlockedUserDto(other.Id, other.Username, other.Role);
        }).OrderBy(x => x.Username).ToList();

        return Ok(list);
    }

    /// <summary>Bir kullanıcıyı engeller (varsa mevcut arkadaşlığı/istekleri keser).</summary>
    [HttpPost("blocks/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> BlockUser(uint id)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id))
            throw new ApiException(404, "Kullanıcı bulunamadı.");

        var existing = await db.Friendships.FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (existing is null)
        {
            db.Friendships.Add(new Friendship
            {
                UserLow = low,
                UserHigh = high,
                RequestedBy = me.Id,
                Status = FriendshipStatuses.Blocked,
                BlockedBy = me.Id
            });
        }
        else
        {
            if (existing.Status == FriendshipStatuses.Blocked && existing.BlockedBy != me.Id)
                throw new ApiException(403, "Bu ilişki zaten engellenmiş durumda.");
            existing.Status = FriendshipStatuses.Blocked;
            existing.BlockedBy = me.Id;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Daha önce engellediğin kullanıcının engelini kaldırır.</summary>
    [HttpDelete("blocks/{id:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> UnblockUser(uint id)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        var existing = await db.Friendships.FirstOrDefaultAsync(f =>
            f.UserLow == low && f.UserHigh == high && f.Status == FriendshipStatuses.Blocked && f.BlockedBy == me.Id);
        if (existing != null)
        {
            db.Friendships.Remove(existing);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    /// <summary>Kabul edilmiş arkadaşla olan mesaj geçmişini sayfalı şekilde döner.</summary>
    [HttpGet("friends/{id:int}/messages")]
    public async Task<ActionResult<MessageHistoryDto>> GetMessages(
        uint id,
        [FromQuery] uint? before = null,
        [FromQuery] uint? after = null)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);
        if (before.HasValue && after.HasValue)
            throw new ApiException(400, "Tek bir mesaj geçmişi konumu belirt.");

        var friendship = await db.Friendships.AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (friendship?.Status != FriendshipStatuses.Accepted)
            throw new ApiException(403, "Bu sohbet için arkadaşlık isteğinin kabul edilmesi gerekiyor.");

        var query = db.Messages.AsNoTracking()
            .Include(m => m.Manga)
            .Where(m => m.UserLow == low && m.UserHigh == high);

        if (before.HasValue) query = query.Where(m => m.Id < before.Value);
        if (after.HasValue) query = query.Where(m => m.Id > after.Value);

        var ordered = after.HasValue
            ? query.OrderBy(m => m.Id)
            : query.OrderByDescending(m => m.Id);

        var rows = await ordered
            .Take(51)
            .Select(m => new MessageDto(m.Id, m.SenderId, m.Body, m.MangaId, m.Manga != null ? m.Manga.Title : null, m.CreatedAt))
            .ToListAsync();

        var hasMore = rows.Count > 50;
        var messages = rows.Take(50).ToList();
        if (!after.HasValue) messages.Reverse();

        return Ok(new MessageHistoryDto(messages, hasMore));
    }

    /// <summary>Kabul edilmiş arkadaşa metin mesajı ve/veya manga paylaşımı gönderir.</summary>
    [HttpPost("friends/{id:int}/messages")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> SendMessage(uint id, [FromBody] SendMessageRequest input)
    {
        var me = HttpContext.RequireCurrentUser();
        var (low, high) = Pair(me.Id, id);

        var body = (input.Body ?? "").Trim();
        var mangaId = input.MangaId is > 0 ? input.MangaId : null;
        if ((string.IsNullOrEmpty(body) && mangaId is null) || body.Length > 2000)
            throw new ApiException(400, "Mesaj metni veya manga seç. Metin en fazla 2000 karakter olabilir.");

        if (mangaId.HasValue && !await db.Mangas.AsNoTracking().AnyAsync(m => m.Id == mangaId.Value))
            throw new ApiException(404, "Paylaşılacak manga bulunamadı.");

        var friendship = await db.Friendships.AsNoTracking()
            .FirstOrDefaultAsync(f => f.UserLow == low && f.UserHigh == high);
        if (friendship?.Status != FriendshipStatuses.Accepted)
            throw new ApiException(403, "Bu sohbet için arkadaşlık isteğinin kabul edilmesi gerekiyor.");

        db.Messages.Add(new Message
        {
            UserLow = low,
            UserHigh = high,
            SenderId = me.Id,
            Body = string.IsNullOrEmpty(body) ? null : body,
            MangaId = mangaId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created);
    }
}
