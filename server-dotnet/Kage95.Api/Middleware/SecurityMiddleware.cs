using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Kage95.Api.Data;
using Kage95.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kage95.Api.Middleware;

public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = ex.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { error = ex.Message });
            }
        }
        catch (BadHttpRequestException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { error = "İstek okunamadı." });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API error on {Method} {Path}", context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { error = "Sunucu işlemi tamamlayamadı." });
            }
        }
    }
}

public partial class SecurityAndSessionMiddleware(RequestDelegate next)
{
    public const string SessionCookieName = "kage95_session";
    public const string UserItemKey = "Kage95User";

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex TokenRegex();

    public static bool IsValidToken(string? token) => !string.IsNullOrEmpty(token) && TokenRegex().IsMatch(token);

    public static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public async Task InvokeAsync(HttpContext context, Kage95DbContext db)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers["Cache-Control"] = "no-store";

            var method = context.Request.Method;
            var isWrite = !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method);
            if (isWrite && context.Request.Headers["X-Kage95"].ToString() != "1")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "İstek doğrulanamadı." });
                return;
            }

            if (context.Request.Cookies.TryGetValue(SessionCookieName, out var token) && IsValidToken(token))
            {
                var hash = HashToken(token);
                var now = DateTime.UtcNow;
                var session = await db.Sessions
                    .AsNoTracking()
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(s => s.TokenHash == hash && s.ExpiresAt > now);
                if (session?.User != null)
                {
                    context.Items[UserItemKey] = session.User;
                }
            }
        }

        await next(context);
    }
}

public static class HttpContextUserExtensions
{
    public static User? GetCurrentUser(this HttpContext context)
        => context.Items.TryGetValue(SecurityAndSessionMiddleware.UserItemKey, out var value) ? value as User : null;

    public static User RequireCurrentUser(this HttpContext context)
        => context.GetCurrentUser() ?? throw new ApiException(StatusCodes.Status401Unauthorized, "Bu işlem için giriş yap.");

    public static User RequireStaff(this HttpContext context)
    {
        var user = context.RequireCurrentUser();
        if (!UserRoles.IsStaff(user.Role))
            throw new ApiException(StatusCodes.Status403Forbidden, "Bu işlem için moderatör veya admin yetkisi gerekiyor.");
        return user;
    }

    public static User RequireAdmin(this HttpContext context)
    {
        var user = context.RequireCurrentUser();
        if (user.Role != UserRoles.Admin)
            throw new ApiException(StatusCodes.Status403Forbidden, "Bu işlem için tam admin yetkisi gerekiyor.");
        return user;
    }
}
