namespace Munchkin.Api.Models
{
    public class Match
    {
        public int Id { get; set; }
        public int? SeasionId { get; set; }
        public Season? Season { get; set; }
        
        public string RoomCode { get; set; } = string.Empty;
        public bool IsOfficial { get; set; } = false;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? FinishedAt {  get; set; }
        
        public int? WinnerUserId {  get; set; }
        public User? WinnerUser { get; set; }

        public ICollection<MatchPlayer> Players { get; set; } = new List<MatchPlayer>();
    }
}
