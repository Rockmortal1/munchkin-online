namespace Munchkin.Api.Models
{
    public class Season
    {
        public int Id {  get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int MinPlayersOfficial { get; set; } = 5;

        public ICollection<Match> Matches { get; set; } = new List<Match>();
    }
}
