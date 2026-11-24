using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Controllers;

/// <summary>Kullanıcı kayıt, giriş, oturum ve çıkış işlemleri.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public partial class AuthController(
    Kage95DbContext db,
    PasswordService passwords,
    PointsService points
) : ControllerBase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[a-z0-9_]{3,24}$")]
    private static partial Regex UsernameRegex();

    /// <summary>Aktif oturumdaki kullanıcı bilgisini döner.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<AuthMeResponse>> Me()
    {
        var user = HttpContext.GetCurrentUser();
        if (user is null) return Ok(new AuthMeResponse(null, 0));

        var earned = await points.AwardDailyLoginAsync(user.Id);
        return Ok(new AuthMeResponse(ToDto(user), earned));
    }

    /// <summary>Yeni kullanıcı hesabı oluşturur ve oturum açar.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest input)
    {
        var firstName = (input.FirstName ?? "").Trim();
        var lastName = (input.LastName ?? "").Trim();
        var email = (input.Email ?? "").Trim().ToLowerInvariant();
        var username = (input.Username ?? "").Trim().ToLowerInvariant();
        var password = input.Password ?? "";

        if (firstName.Length is < 1 or > 60 || lastName.Length is < 1 or > 60)
            throw new ApiException(400, "Ad ve soyad 1–60 karakter olmalı.");
        if (email.Length > 254 || !EmailRegex().IsMatch(email))
            throw new ApiException(400, "Geçerli bir e-posta gir.");
        if (!UsernameRegex().IsMatch(username))
            throw new ApiException(400, "Kullanıcı adı 3–24 harf, rakam veya alt çizgi içermeli.");
        if (password.Length is < 10 or > 128)
            throw new ApiException(400, "Şifre 10–128 karakter olmalı.");

        var exists = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Email == email || u.Username == username);
        if (exists)
            throw new ApiException(409, "Bu e-posta veya kullanıcı adı zaten kullanılıyor.");

        var hash = await passwords.HashPasswordAsync(password);
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Username = username,
            PasswordHash = hash,
            Role = UserRoles.User,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new ApiException(409, "Bu e-posta veya kullanıcı adı zaten kullanılıyor.");
        }

        await StartSessionAsync(user.Id);
        var earned = await points.AwardDailyLoginAsync(user.Id);
        return StatusCode(StatusCodes.Status201Created, new AuthResponse(ToDto(user), earned));
    }

    /// <summary>E-posta veya kullanıcı adı ve şifre ile giriş yapar.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest input)
    {
        var login = (input.Login ?? "").Trim().ToLowerInvariant();
        var password = input.Password ?? "";
        if (login.Length is < 1 or > 254 || password.Length > 128)
            throw new ApiException(400, "Giriş bilgilerini kontrol et.");

        var user = login.Contains('@')
            ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == login)
            : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == login);

        var ok = await passwords.VerifyPasswordAsync(password, user?.PasswordHash);
        if (!ok || user is null)
            throw new ApiException(401, "Giriş bilgileri hatalı.");

        await StartSessionAsync(user.Id);
        var earned = await points.AwardDailyLoginAsync(user.Id);
        return Ok(new AuthResponse(ToDto(user), earned));
    }

    /// <summary>Aktif oturumu sonlandırır ve çerezi temizler.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(SecurityAndSessionMiddleware.SessionCookieName, out var token)
            && SecurityAndSessionMiddleware.IsValidToken(token))
        {
            var hash = SecurityAndSessionMiddleware.HashToken(token);
            var existing = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == hash);
            if (existing != null)
            {
                db.Sessions.Remove(existing);
                await db.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete(SecurityAndSessionMiddleware.SessionCookieName, BuildCookieOptions());
        return NoContent();
    }

    private async Task StartSessionAsync(uint userId)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = SecurityAndSessionMiddleware.HashToken(token);

        db.Sessions.Add(new Session
        {
            TokenHash = hash,
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.Add(SessionLifetime)
        });

        if (Request.Cookies.TryGetValue(SecurityAndSessionMiddleware.SessionCookieName, out var prev)
            && SecurityAndSessionMiddleware.IsValidToken(prev))
        {
            var prevHash = SecurityAndSessionMiddleware.HashToken(prev);
            var oldSession = await db.Sessions.FirstOrDefaultAsync(s => s.TokenHash == prevHash);
            if (oldSession != null) db.Sessions.Remove(oldSession);
        }

        await db.SaveChangesAsync();

        var options = BuildCookieOptions();
        options.MaxAge = SessionLifetime;
        Response.Cookies.Append(SecurityAndSessionMiddleware.SessionCookieName, token, options);
    }

    private CookieOptions BuildCookieOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = Request.IsHttps,
        Path = "/"
    };

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Username, u.FirstName, u.LastName, u.Email, u.Role, u.Bio, u.AvatarUrl);
}
