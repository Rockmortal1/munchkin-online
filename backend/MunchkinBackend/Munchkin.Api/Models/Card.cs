using System.ComponentModel.DataAnnotations.Schema;

namespace Munchkin.Api.Models
{
    public class Card
    {
        public int Id { get; set; }
        public int PackId { get; set; }
        public CardPack Pack { get; set; } = null;

        public string Name { get; set; } = string.Empty;
        public string DeckType {  get; set; } = string.Empty;
        public string CardType {  get; set; } = string.Empty;
        public string Description {  get; set; } = string.Empty;
        public string? ImageUrl { get; set; }

        [Column(TypeName = "jsonb")]
        public string Stats { get; set; } = "{}";
    }
}
