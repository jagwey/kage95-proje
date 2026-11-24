using Kage95.Api.DTOs;
using Kage95.Api.Middleware;
using Kage95.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kage95.Api.Controllers;

/// <summary>KAGE95 liderlik tablosu, rütbe/seviye sıralaması ve puan kuralları.</summary>
[ApiController]
[Route("api/leaderboard")]
[Tags("Leaderboard (Liderlik)")]
public class LeaderboardController(PointsService points) : ControllerBase
{
    /// <summary>Liderlik tablosunu getirir. period: tum | ay | hafta (varsayılan: tum).</summary>
    [HttpGet]
    public async Task<ActionResult<LeaderboardResponseDto>> GetLeaderboard(
        [FromQuery] string? period = "tum",
        [FromQuery] int limit = 50)
    {
        var current = HttpContext.GetCurrentUser();
        var board = await points.GetLeaderboardAsync(period, limit, current?.Id);
        return Ok(board);
    }
}
