namespace Munchkin.Api.Models;

public class MatchRecord
{
    public int Id { get; set; }
    public string SeasonName { get; set; } = "Temporada Primavera 2026";
    public string RoomCode { get; set; } = string.Empty;
    public string WinnerUsername { get; set; } = string.Empty;
    public string PlayersList { get; set; } = string.Empty;
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}