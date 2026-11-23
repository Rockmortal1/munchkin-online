using Microsoft.AspNetCore.SignalR;
using Munchkin.Api.GameEngine;
using Munchkin.Api.Data;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;

namespace Munchkin.Api.Hubs
{
    public class GameHub : Hub
    {
        private readonly GameRoomManager _roomManager;
        private readonly AppDbContext _db;

        public GameHub(GameRoomManager roomManager, AppDbContext db)
        {
            _roomManager = roomManager;
            _db = db;
        }

        public async Task CreateRoom (string username)
        {
            var room = _roomManager.CreateRoom(Context.ConnectionId, username);
            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task JoinRoom(string roomCode, string username)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null)
            {
                await Clients.Caller.SendAsync("Error", "No existe ninguna sala con ese codigo");
                return;
            }

            if (room.IsStarted)
            {
                await Clients.Caller.SendAsync("Error", "La partida ya comenzo.");
                return;
            }

            room.Players.Add(new PlayerState
            {
                ConnectionId = Context.ConnectionId,
                Username = username
            });
            room.ActionLog.Add($"{username} se unio a la sala");

            await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomCode);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task RollDice(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null) return;

            int result = Random.Shared.Next(1, 7);
            room.ActionLog.Add($"{player.Username} tiro el dado y saco un {result}.");

            await Clients.Group(room.RoomCode).SendAsync("DiceRolled", player.Username, result);
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task StartGame(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null || room.IsStarted) return;

            if (room.HostConnectionId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("Error", "Solo el creador de la sala puede iniciar la partida.");
                return;
            }

            // Traer IDs de cartas de PostgreSQL y mezclarlos al azar
            var doorIds = _db.Cards.Where(c => c.DeckType == "door").Select(c => c.Id).ToList();
            var treasureIds = _db.Cards.Where(c => c.DeckType == "treasure").Select(c => c.Id).ToList();

            room.DoorDeck = doorIds.OrderBy(_ => Random.Shared.Next()).ToList();
            room.TreasureDeck = treasureIds.OrderBy(_ => Random.Shared.Next()).ToList();

            // Repartir 4 Puertas y 4 Tesoros a cada jugador
            foreach (var player in room.Players)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (room.DoorDeck.Count > 0)
                    {
                        player.HandCardIds.Add(room.DoorDeck[0]);
                        room.DoorDeck.RemoveAt(0);
                    }
                    if (room.TreasureDeck.Count > 0)
                    {
                        player.HandCardIds.Add(room.TreasureDeck[0]);
                        room.TreasureDeck.RemoveAt(0);
                    }
                }
            }

            room.IsStarted = true;
            room.ActionLog.Add("¡La partida comienza! Se repartieron 4 Puertas y 4 Tesoros a cada jugador.");

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task ChangeLevel(string roomCode, int delta)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null) return;

            player.Level = Math.Clamp(player.Level + delta, 1, 10);
            room.ActionLog.Add($"{player.Username} ahora es nivel {player.Level}.");

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }
        
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var room = _roomManager.RemovePlayerByConnection(Context.ConnectionId, out _);
            if (room != null && room.Players.Count > 0)
            {
                await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task PlayCard(string roomCode, int cardId, string? targetConnectionId = null)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null || !player.HandCardIds.Contains(cardId)) return;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return;

            if (room.ActiveCombat != null && (card.CardType == "item" || card.CardType == "race" || card.CardType == "class"))
            {
                await Clients.Caller.SendAsync("Error", "No podés equiparte ítems, razas ni clases durante un combate.");
                return;
            }

            // 1. Bloquear monstruos directos desde la mano
            if (card.CardType == "monster")
            {
                await Clients.Caller.SendAsync("Error", "No podés bajar un monstruo directo de la mano (requiere Buscar Problemas o Monstruo Errante).");
                return;
            }

            if (card.CardType == "item" || card.CardType == "one_shot")
            {
                var slotError = ValidateEquipmentSlot(player, card);
                if (slotError != null)
                {
                    await Clients.Caller.SendAsync("Error", slotError);
                    return;
                }
            }

            player.HandCardIds.Remove(cardId);

            // 2. Razas y Clases se equipan en mesa (y reemplazan la anterior si ya tenías una)
            if (card.CardType == "race" || card.CardType == "class")
            {
                var existingCardIds = _db.Cards
                    .Where(c => player.EquippedCardIds.Contains(c.Id) && c.CardType == card.CardType)
                    .Select(c => c.Id)
                    .ToList();

                foreach (var oldId in existingCardIds)
                {
                    player.EquippedCardIds.Remove(oldId);
                    room.DoorDiscard.Add(oldId);
                }

                player.EquippedCardIds.Add(cardId);
                if (card.CardType == "race") player.Race = card.Name;
                else player.Class = card.Name;

                room.ActionLog.Add($"{player.Username} se equipó {card.Name}.");
            }
            // 3. Maldiciones: se aplican al jugador elegido (targetConnectionId)
            else if (card.CardType == "curse")
            {
                var target = room.Players.FirstOrDefault(p => p.ConnectionId == targetConnectionId) ?? player;
                try
                {
                    using var doc = JsonDocument.Parse(card.Stats);
                    if (doc.RootElement.TryGetProperty("levelDelta", out var deltaProp))
                    {
                        target.Level = Math.Clamp(target.Level + deltaProp.GetInt32(), 1, 10);
                    }
                }
                catch { }

                room.DoorDiscard.Add(cardId);
                room.ActionLog.Add($"{player.Username} le lanzó '{card.Name}' a {target.Username} (ahora Nivel {target.Level}).");
            }
            // 4. Subir Nivel
            else if (card.CardType == "level_up")
            {
                if (player.Level < 9)
                {
                    player.Level++;
                    room.ActionLog.Add($"{player.Username} usó '{card.Name}' y subió a Nivel {player.Level}!");
                }
                else
                {
                    room.ActionLog.Add($"{player.Username} descartó '{card.Name}' (no sirve para llegar al nivel 10).");
                }
                room.TreasureDiscard.Add(cardId);
            }
            // 5. Objetos y One-Shot se bajan al equipo en mesa (E.t)
            else if (card.CardType == "item" || card.CardType == "one_shot")
            {
                player.EquippedCardIds.Add(cardId);
                RecalculateGearBonus(player);
                room.ActionLog.Add($"{player.Username} bajó a mesa: {card.Name}.");
            }
            else
            {
                if (card.DeckType == "door") room.DoorDiscard.Add(cardId);
                else room.TreasureDiscard.Add(cardId);
                room.ActionLog.Add($"{player.Username} jugó '{card.Name}'.");
            }

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task UseEquippedCard(string roomCode, int cardId)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null || !player.EquippedCardIds.Contains(cardId)) return;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return;

            player.EquippedCardIds.Remove(cardId);
            RecalculateGearBonus(player);

            if (card.DeckType == "door") room.DoorDiscard.Add(cardId);
            else room.TreasureDiscard.Add(cardId);

            room.ActionLog.Add($"{player.Username} USÓ '{card.Name}' desde su equipo!");
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task DiscardCard(string roomCode, int cardId, bool fromEquipped)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null) return;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return;

            if (fromEquipped)
            {
                if (!player.EquippedCardIds.Remove(cardId)) return;
                if (card.CardType == "race") player.Race = "Humano";
                if (card.CardType == "class") player.Class = "Ninguna";
                RecalculateGearBonus(player);
            }
            else
            {
                if (!player.HandCardIds.Remove(cardId)) return;
            }

            if (card.DeckType == "door") room.DoorDiscard.Add(cardId);
            else room.TreasureDiscard.Add(cardId);

            room.ActionLog.Add($"{player.Username} descartó '{card.Name}'.");
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        private void RecalculateGearBonus(PlayerState player)
        {
            var equippedCards = _db.Cards.Where(c => player.EquippedCardIds.Contains(c.Id)).ToList();
            int totalBonus = 0;

            foreach (var c in equippedCards)
            {
                if (c.CardType != "item") continue; // Los one_shot en mesa no suman hasta usarse en combate
                try
                {
                    using var doc = JsonDocument.Parse(c.Stats);
                    if (doc.RootElement.TryGetProperty("bonus", out var bonusProp))
                    {
                        totalBonus += bonusProp.GetInt32();
                    }
                }
                catch { }
            }

            player.GearBonus = totalBonus;
        }

        public async Task KickDoor(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null || !room.IsStarted || room.ActiveCombat != null || room.HasKickedDoor) return;

            var currentPlayer = room.Players[room.CurrentTurnIndex];
            if (currentPlayer.ConnectionId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("Error", "No es tu turno para patear la puerta.");
                return;
            }

            if (room.DoorDeck.Count == 0)
            {
                // Si se acabó el mazo, reciclamos el descarte
                room.DoorDeck = room.DoorDiscard.OrderBy(_ => Random.Shared.Next()).ToList();
                room.DoorDiscard.Clear();
            }
            if (room.DoorDeck.Count == 0) return;

            int cardId = room.DoorDeck[0];
            room.DoorDeck.RemoveAt(0);
            room.HasKickedDoor = true;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return;

            if (card.CardType == "monster")
            {
                StartCombatWithMonster(room, currentPlayer, card);
                room.ActionLog.Add($"¡{currentPlayer.Username} pateó la puerta y apareció {card.Name}!");
            }
            else if (card.CardType == "curse")
            {
                try
                {
                    using var doc = JsonDocument.Parse(card.Stats);
                    if (doc.RootElement.TryGetProperty("levelDelta", out var d))
                        currentPlayer.Level = Math.Clamp(currentPlayer.Level + d.GetInt32(), 1, 10);
                }
                catch { }
                room.DoorDiscard.Add(cardId);
                room.ActionLog.Add($"¡{currentPlayer.Username} pateó la puerta y ligó '{card.Name}'!");
            }
            else
            {
                currentPlayer.HandCardIds.Add(cardId);
                room.ActionLog.Add($"{currentPlayer.Username} pateó la puerta y se llevó '{card.Name}' a la mano.");
            }

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task LookForTrouble(string roomCode, int cardId)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null || !room.IsStarted || !room.HasKickedDoor || room.ActiveCombat != null) return;

            var currentPlayer = room.Players[room.CurrentTurnIndex];
            if (currentPlayer.ConnectionId != Context.ConnectionId || !currentPlayer.HandCardIds.Contains(cardId)) return;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null || card.CardType != "monster") return;

            currentPlayer.HandCardIds.Remove(cardId);
            StartCombatWithMonster(room, currentPlayer, card);
            room.ActionLog.Add($"{currentPlayer.Username} buscó problemas y bajó a {card.Name}!");

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task PlayCombatModifier(string roomCode, int cardId, string side, bool fromEquipped)
        {
            var room = _roomManager.GetRoom(roomCode);
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || player == null || room.ActiveCombat == null) return;

            var card = _db.Cards.FirstOrDefault(c => c.Id == cardId);
            if (card == null) return;

            if (fromEquipped)
            {
                if (!player.EquippedCardIds.Remove(cardId)) return;
                RecalculateGearBonus(player);
            }
            else
            {
                if (!player.HandCardIds.Remove(cardId)) return;
            }

            int bonus = 0;
            try
            {
                using var doc = JsonDocument.Parse(card.Stats);
                if (doc.RootElement.TryGetProperty("combatBonus", out var cb)) bonus = cb.GetInt32();
                if (doc.RootElement.TryGetProperty("extraTreasures", out var et)) room.ActiveCombat.MonsterTreasures += et.GetInt32();
            }
            catch { }

            if (side == "player")
            {
                room.ActiveCombat.PlayerSideModifier += bonus;
                room.ActionLog.Add($"{player.Username} usó '{card.Name}' (+{bonus} a Jugadores).");
            }
            else
            {
                room.ActiveCombat.MonsterSideModifier += bonus;
                room.ActionLog.Add($"{player.Username} usó '{card.Name}' (+{bonus} al Monstruo).");
            }

            if (card.DeckType == "door") room.DoorDiscard.Add(cardId);
            else room.TreasureDiscard.Add(cardId);

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task AdjustCombatModifier(string roomCode, string side, int delta)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room?.ActiveCombat == null) return;

            if (side == "player") room.ActiveCombat.PlayerSideModifier += delta;
            else room.ActiveCombat.MonsterSideModifier += delta;

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task ResolveCombat(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            var combat = room?.ActiveCombat;
            if (room == null || combat == null) return;

            var player = room.Players.FirstOrDefault(p => p.ConnectionId == combat.ActivePlayerConnectionId);
            if (player == null || player.ConnectionId != Context.ConnectionId) return;

            var helper = combat.HelperConnectionId != null
                ? room.Players.FirstOrDefault(p => p.ConnectionId == combat.HelperConnectionId)
                : null;

            int playerPower = player.Level + player.GearBonus + combat.PlayerSideModifier;
            if (helper != null) playerPower += helper.Level + helper.GearBonus;

            int monsterPower = combat.MonsterBaseLevel + combat.MonsterSideModifier;

            bool hasWarrior = player.Class == "Guerrero" || helper?.Class == "Guerrero";
            bool wins = hasWarrior ? playerPower >= monsterPower : playerPower > monsterPower;

            if (!wins)
            {
                await Clients.Caller.SendAsync("Error", $"Estás perdiendo ({playerPower} vs {monsterPower}). Pedí ayuda, tirá modificadores o apretá Huir.");
                return;
            }

            // Subir niveles
            player.Level = Math.Clamp(player.Level + combat.MonsterLevelsGained, 1, 10);
            if (helper != null && helper.Race.Trim().Equals("Elfo", StringComparison.OrdinalIgnoreCase))
            {
                helper.Level = Math.Clamp(helper.Level + 1, 1, 10);
                room.ActionLog.Add($"🧝 {helper.Username} subió a Nivel {helper.Level} por ayudar siendo Elfo!");
            }


            // Chequear victoria en Nivel 10
            if (player.Level >= 10 || (helper != null && helper.Level >= 10))
            {
                room.WinnerUsername = player.Level >= 10 ? player.Username : helper!.Username;
                room.ActiveCombat = null;
                room.ActionLog.Add($"👑 ¡{room.WinnerUsername} LLEGÓ A NIVEL 10 Y GANÓ LA PARTIDA!");
                await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
                return;
            }

            // Repartir tesoros (primero la parte del ayudante, el resto al jugador activo)
            int helperShare = helper != null ? Math.Clamp(combat.HelperTreasureShare, 0, combat.MonsterTreasures) : 0;
            int playerShare = combat.MonsterTreasures - helperShare;

            void GiveTreasures(PlayerState target, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (room.TreasureDeck.Count == 0 && room.TreasureDiscard.Count > 0)
                    {
                        room.TreasureDeck = room.TreasureDiscard.OrderBy(_ => Random.Shared.Next()).ToList();
                        room.TreasureDiscard.Clear();
                    }
                    if (room.TreasureDeck.Count > 0)
                    {
                        target.HandCardIds.Add(room.TreasureDeck[0]);
                        room.TreasureDeck.RemoveAt(0);
                    }
                }
            }

            if (helper != null && helperShare > 0) GiveTreasures(helper, helperShare);
            GiveTreasures(player, playerShare);

            room.DoorDiscard.AddRange(combat.MonsterCardIds);
            room.ActionLog.Add($"🏆 ¡Victoria ({playerPower} vs {monsterPower})! {player.Username} subió a Nvl {player.Level} ({playerShare} tesoros)" +
                (helper != null ? $" y {helper.Username} recibió {helperShare} tesoro(s)." : "."));
            room.ActiveCombat = null;

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task RunAway(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room?.ActiveCombat == null) return;

            var player = room.Players.FirstOrDefault(p => p.ConnectionId == room.ActiveCombat.ActivePlayerConnectionId);
            if (player == null || player.ConnectionId != Context.ConnectionId) return;

            int roll = Random.Shared.Next(1, 7);
            room.DoorDiscard.AddRange(room.ActiveCombat.MonsterCardIds);
            room.ActiveCombat = null;

            if (roll >= 5)
                room.ActionLog.Add($"{player.Username} tiró un {roll} y logró HUIR sin rasguños!");
            else
                room.ActionLog.Add($"{player.Username} tiró un {roll} y NO pudo huir. ¡A aplicar la cosa mala!");

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task EndTurn(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null || !room.IsStarted || room.ActiveCombat != null) return;

            var currentPlayer = room.Players[room.CurrentTurnIndex];
            if (currentPlayer.ConnectionId != Context.ConnectionId) return;

            int maxHand = currentPlayer.Race == "Enano" ? 6 : 5;
            if (currentPlayer.HandCardIds.Count > maxHand)
            {
                await Clients.Caller.SendAsync("Error", $"Caridad: tenés {currentPlayer.HandCardIds.Count} cartas en mano (máximo {maxHand}). Descartá o regalá cartas antes de pasar turno.");
                return;
            }

            room.CurrentTurnIndex = (room.CurrentTurnIndex + 1) % room.Players.Count;
            room.HasKickedDoor = false;

            var nextPlayer = room.Players[room.CurrentTurnIndex];
            room.ActionLog.Add($"Turno finalizado. Ahora es el turno de {nextPlayer.Username}.");

            foreach (var p in room.Players)
                p.StolenTargetsThisTurn.Clear();

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        private static void StartCombatWithMonster(GameRoom room, PlayerState player, Models.Card monster)
        {
            int level = 1, treasures = 1, levelsGained = 1;
            try
            {
                using var doc = JsonDocument.Parse(monster.Stats);
                if (doc.RootElement.TryGetProperty("level", out var l)) level = l.GetInt32();
                if (doc.RootElement.TryGetProperty("treasures", out var t)) treasures = t.GetInt32();
                if (doc.RootElement.TryGetProperty("levelsGained", out var lg)) levelsGained = lg.GetInt32();
            }
            catch { }

            room.ActiveCombat = new CombatState
            {
                ActivePlayerConnectionId = player.ConnectionId,
                MonsterCardIds = new List<int> { monster.Id },
                MonsterBaseLevel = level,
                MonsterTreasures = treasures,
                MonsterLevelsGained = levelsGained
            };
        }

        public async Task ThiefSteal(string roomCode, int discardCardId, string targetConnectionId, int targetCardId)
        {
            var room = _roomManager.GetRoom(roomCode);
            var thief = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            var target = room?.Players.FirstOrDefault(p => p.ConnectionId == targetConnectionId);

            if (room == null || thief == null || target == null || room.WinnerUsername != null) return;

            if (room.ActiveCombat != null)
            {
                await Clients.Caller.SendAsync("Error", "No podés robar mientras hay un combate activo.");
                return;
            }

            if (thief.Class != "Ladrón")
            {
                await Clients.Caller.SendAsync("Error", "Solo un Ladrón puede usar esta habilidad.");
                return;
            }

            if (thief.StolenTargetsThisTurn.Contains(targetConnectionId))
            {
                await Clients.Caller.SendAsync("Error", $"Ya intentaste robarle a {target.Username} en este turno (máximo 1 intento por jugador).");
                return;
            }

            if (!thief.HandCardIds.Contains(discardCardId) || !target.EquippedCardIds.Contains(targetCardId)) return;

            var discardCard = _db.Cards.FirstOrDefault(c => c.Id == discardCardId);
            var targetCard = _db.Cards.FirstOrDefault(c => c.Id == targetCardId);
            if (discardCard == null || targetCard == null) return;

            if (targetCard.CardType == "race" || targetCard.CardType == "class")
            {
                await Clients.Caller.SendAsync("Error", "No podés robar razas ni clases.");
                return;
            }

            // 1. Pagar el costo: descartar 1 carta de la mano
            thief.HandCardIds.Remove(discardCardId);
            if (discardCard.DeckType == "door") room.DoorDiscard.Add(discardCardId);
            else room.TreasureDiscard.Add(discardCardId);

            // 2. Registrar el intento contra este jugador en este turno
            thief.StolenTargetsThisTurn.Add(targetConnectionId);

            // 3. Tirar el dado (5-6 roba, 4 o menos pierde 1 nivel sin bajar de 1)
            int roll = Random.Shared.Next(1, 7);
            if (roll >= 5)
            {
                target.EquippedCardIds.Remove(targetCardId);
                RecalculateGearBonus(target);
                thief.HandCardIds.Add(targetCardId);
                room.ActionLog.Add($"🥷 ¡{thief.Username} sacó un {roll} y le ROBÓ '{targetCard.Name}' a {target.Username}!");
            }
            else
            {
                thief.Level = Math.Clamp(thief.Level - 1, 1, 10);
                room.ActionLog.Add($"🚨 {thief.Username} sacó un {roll} intentando robarle a {target.Username} y falló (Nivel actual: {thief.Level}).");
            }

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task StartTrade(string roomCode, string targetConnectionId)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room == null || room.WinnerUsername != null) return;

            if (room.ActiveCombat != null)
            {
                await Clients.Caller.SendAsync("Error", "No podés negociar mientras hay un combate activo.");
                return;
            }

            if (room.ActiveTrade != null)
            {
                await Clients.Caller.SendAsync("Error", "Ya hay una negociación en curso en la mesa.");
                return;
            }

            var proposer = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            var target = room.Players.FirstOrDefault(p => p.ConnectionId == targetConnectionId);
            if (proposer == null || target == null || proposer.ConnectionId == target.ConnectionId) return;

            room.ActiveTrade = new TradeState
            {
                ProposerConnectionId = proposer.ConnectionId,
                TargetConnectionId = target.ConnectionId
            };

            room.ActionLog.Add($"🤝 {proposer.Username} abrió una negociación con {target.Username}.");
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task ToggleTradeCard(string roomCode, int cardId)
        {
            var room = _roomManager.GetRoom(roomCode);
            var trade = room?.ActiveTrade;
            var player = room?.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (room == null || trade == null || player == null) return;

            // Verificar que la carta realmente pertenezca al jugador (mano o equipo)
            bool ownsCard = player.HandCardIds.Contains(cardId) || player.EquippedCardIds.Contains(cardId);
            if (!ownsCard) return;

            List<int>? list = null;
            if (trade.ProposerConnectionId == player.ConnectionId) list = trade.ProposerCardIds;
            else if (trade.TargetConnectionId == player.ConnectionId) list = trade.TargetCardIds;
            if (list == null) return;

            if (list.Contains(cardId)) list.Remove(cardId);
            else list.Add(cardId);

            // Anti-estafa: si cambia la oferta, ambos deben volver a aceptar
            trade.ProposerAccepted = false;
            trade.TargetAccepted = false;

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task AcceptTrade(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            var trade = room?.ActiveTrade;
            if (room == null || trade == null) return;

            if (trade.ProposerConnectionId == Context.ConnectionId) trade.ProposerAccepted = true;
            else if (trade.TargetConnectionId == Context.ConnectionId) trade.TargetAccepted = true;
            else return;

            if (trade.ProposerAccepted && trade.TargetAccepted)
            {
                var proposer = room.Players.FirstOrDefault(p => p.ConnectionId == trade.ProposerConnectionId);
                var target = room.Players.FirstOrDefault(p => p.ConnectionId == trade.TargetConnectionId);

                if (proposer != null && target != null)
                {
                    TransferTradeCards(proposer, target, trade.ProposerCardIds);
                    TransferTradeCards(target, proposer, trade.TargetCardIds);

                    RecalculateGearBonus(proposer);
                    RecalculateGearBonus(target);

                    room.ActionLog.Add($"✅ ¡Trato cerrado entre {proposer.Username} ({trade.ProposerCardIds.Count} cartas) y {target.Username} ({trade.TargetCardIds.Count} cartas)!");
                }

                room.ActiveTrade = null;
            }

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task CancelTrade(string roomCode)
        {
            var room = _roomManager.GetRoom(roomCode);
            if (room?.ActiveTrade == null) return;

            room.ActiveTrade = null;
            room.ActionLog.Add("❌ Negociación cancelada.");
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        private void TransferTradeCards(PlayerState from, PlayerState to, List<int> cardIds)
        {
            foreach (var cid in cardIds)
            {
                if (from.HandCardIds.Remove(cid) || from.EquippedCardIds.Remove(cid))
                {
                    var card = _db.Cards.FirstOrDefault(c => c.Id == cid);
                    if (card?.CardType == "race") from.Race = "Humano";
                    if (card?.CardType == "class") from.Class = "Ninguna";
                    to.HandCardIds.Add(cid);
                }
            }
        }

        public async Task RequestCombatHelp(string roomCode, string targetConnectionId, int offeredTreasures)
        {
            var room = _roomManager.GetRoom(roomCode);
            var combat = room?.ActiveCombat;
            if (room == null || combat == null || combat.HelperConnectionId != null) return;
            if (combat.ActivePlayerConnectionId != Context.ConnectionId) return;

            var helper = room.Players.FirstOrDefault(p => p.ConnectionId == targetConnectionId);
            var activePlayer = room.Players.FirstOrDefault(p => p.ConnectionId == combat.ActivePlayerConnectionId);
            if (helper == null || activePlayer == null || helper.ConnectionId == activePlayer.ConnectionId) return;

            combat.PendingHelperConnectionId = helper.ConnectionId;
            combat.PendingHelperTreasures = Math.Clamp(offeredTreasures, 0, combat.MonsterTreasures);

            room.ActionLog.Add($"📣 {activePlayer.Username} le pidió ayuda a {helper.Username} ofreciendo {combat.PendingHelperTreasures} tesoro(s).");
            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        public async Task RespondCombatHelp(string roomCode, bool accept)
        {
            var room = _roomManager.GetRoom(roomCode);
            var combat = room?.ActiveCombat;
            if (room == null || combat == null || combat.PendingHelperConnectionId != Context.ConnectionId) return;

            var helper = room.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (helper == null) return;

            if (accept)
            {
                combat.HelperConnectionId = helper.ConnectionId;
                combat.HelperTreasureShare = combat.PendingHelperTreasures;
                room.ActionLog.Add($"🤝 ¡{helper.Username} se sumó al combate por {combat.HelperTreasureShare} tesoro(s)!");
            }
            else
            {
                room.ActionLog.Add($"❌ {helper.Username} rechazó la oferta de ayuda.");
            }

            combat.PendingHelperConnectionId = null;
            combat.PendingHelperTreasures = 0;

            await Clients.Group(room.RoomCode).SendAsync("RoomUpdated", room);
        }

        private string? ValidateEquipmentSlot(PlayerState player, Models.Card newCard)
        {
            var equipped = _db.Cards.Where(c => player.EquippedCardIds.Contains(c.Id)).ToList();

            // Límite de 4 cartas one_shot en mesa
            if (newCard.CardType == "one_shot")
            {
                int oneShots = equipped.Count(c => c.CardType == "one_shot");
                if (oneShots >= 4) return "Ya tenés el máximo de 4 ítems de un solo uso en mesa.";
                return null;
            }

            string GetSlot(Models.Card c)
            {
                try
                {
                    using var doc = JsonDocument.Parse(c.Stats);
                    return doc.RootElement.TryGetProperty("slot", out var s) ? (s.GetString() ?? "").ToLower() : "";
                }
                catch { return ""; }
            }

            bool IsBig(Models.Card c)
            {
                try
                {
                    using var doc = JsonDocument.Parse(c.Stats);
                    return doc.RootElement.TryGetProperty("isBig", out var b) && b.GetBoolean();
                }
                catch { return false; }
            }

            string newSlot = GetSlot(newCard);

            // 1. Control de Objeto Grande (Enano no tiene límite)
            if (IsBig(newCard) && !player.Race.Equals("Enano", StringComparison.OrdinalIgnoreCase))
            {
                if (equipped.Any(IsBig))
                    return "Solo podés llevar 1 Objeto Grande (salvo que seas Enano).";
            }

            // 2. Control de Manos (máximo 2)
            if (newSlot == "1_hand" || newSlot == "2_hands")
            {
                int newHands = newSlot == "2_hands" ? 2 : 1;
                int usedHands = equipped.Sum(c =>
                {
                    var s = GetSlot(c);
                    return s == "2_hands" ? 2 : (s == "1_hand" ? 1 : 0);
                });

                if (usedHands + newHands > 2)
                    return $"No tenés manos libres (usás {usedHands}/2 manos). Descartá un arma primero.";
            }

            // 3. Control de slots únicos (casco, armadura, calzado)
            if (newSlot is "headgear" or "armor" or "footgear")
            {
                if (equipped.Any(c => GetSlot(c) == newSlot))
                    return $"Ya tenés equipado un objeto en el slot '{newSlot}'. Descartá el anterior primero.";
            }

            return null;
        }
    }
}
