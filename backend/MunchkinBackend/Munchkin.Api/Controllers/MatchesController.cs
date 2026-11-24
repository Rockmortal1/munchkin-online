using Microsoft.AspNetCore.Mvc;
using Munchkin.Api.Data;

namespace Munchkin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MatchesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetSeasonStats([FromQuery] string season = "Temporada Primavera 2026")
    {
        var matches = _db.MatchRecords
            .Where(m => m.SeasonName == season)
            .OrderByDescending(m => m.PlayedAt)
            .ToList();

        var leaderboard = matches
            .GroupBy(m => m.WinnerUsername)
            .Select(g => new { username = g.Key, wins = g.Count() })
            .OrderByDescending(x => x.wins)
            .ToList();

        var recent = matches.Take(10).Select(m => new
        {
            m.Id,
            m.SeasonName,
            m.WinnerUsername,
            m.PlayersList,
            m.PlayedAt
        }).ToList();

        return Ok(new { season, leaderboard, recent });
    }
}