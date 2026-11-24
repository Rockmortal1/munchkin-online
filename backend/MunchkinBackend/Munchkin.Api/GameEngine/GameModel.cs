namespace Munchkin.Api.GameEngine
{
    public class GameRoom
    {
        public string RoomCode { get; set; } = string.Empty;
        public string HostConnectionId { get; set; } = string.Empty;
        public bool IsStarted { get; set; } = false;
        public List<int> SelectedPackIds { get; set; } = new();
        public List<PlayerState> Players { get; set; } = new();
        public int CurrentTurnIndex { get; set; } = 0;

        //Mazos y Descartes (Se guarda las Ids de Cartas de la DB)
        public List<int> DoorDeck { get; set; } = new();
        public List<int> TreasureDeck { get; set; } = new();
        public List<int> DoorDiscard { get; set; } = new();
        public List<int> TreasureDiscard { get; set; } = new();

        public CombatState? ActiveCombat {  get; set; }
        public List<string> ActionLog { get; set; } = new();
        public bool HasKickedDoor { get; set; } = false;
        public bool HasFoughtThisTurn { get; set; } = false;
        public bool HasLootedRoom { get; set; } = false;
        public string? WinnerUsername { get; set; }
        public TradeState? ActiveTrade { get; set; }
    }

    public class PlayerState
    {
        public string ConnectionId { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public int GearBonus { get; set; } = 0;
        public string Race {  get; set; } = "Humano";
        public string Class { get; set; } = "Ninguna";

        public List<int> HandCardIds { get; set; } = new();
        public List<int> EquippedCardIds { get; set; } = new();
        public List<string> StolenTargetsThisTurn { get; set; } = new();
        public int GoldBank { get; set; } = 0;
    }

    public class CombatState
    {
        public string ActivePlayerConnectionId { get; set; } = string.Empty;
        public string? HelperConnectionId { get; set; }
        public List<int> MonsterCardIds { get; set; } = new();
        public int MonsterBaseLevel { get; set; } = 0;
        public int MonsterTreasures { get; set; } = 1;
        public int MonsterLevelsGained { get; set; } = 1;
        public int PlayerSideModifier { get; set; } = 0;
        public int MonsterSideModifier { get; set; } = 0;
        public int HelperTreasureShare { get; set; } = 0;
        public string? PendingHelperConnectionId { get; set; }
        public int PendingHelperTreasures { get; set; } = 0;
    }

    public class TradeState
    {
        public string ProposerConnectionId { get; set; } = string.Empty;
        public string TargetConnectionId { get; set; } = string.Empty;
        public List<int> ProposerCardIds { get; set; } = new();
        public List<int> TargetCardIds { get; set; } = new();
        public bool ProposerAccepted { get; set; } = false;
        public bool TargetAccepted { get; set; } = false;
    }
}
