namespace Munchkin.Api.Models
{
    public class CardPack
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsOfficial { get; set; } = false;
        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public ICollection<Card> Cards { get; set; } = new List<Card>();
    }
}
