namespace Munchkin.Api.Models
{
    public class MatchPlayer
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public Match Match { get; set; } = null;

        public int UserId { get; set; }
        public User User { get; set; } = null;

        public int FinalLevel { get; set; } = 1;
        public int MonsterKilled { get; set; } = 0;
        public int Deaths { get; set; } = 0;
        public int PointsEarned { get; set; } = 0;
    }
}
