using System.Collections.Concurrent;

namespace Munchkin.Api.GameEngine
{
    public class GameRoomManager
    {
        private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();

        public GameRoom CreateRoom(string hostConnectionId, string hostUsername)
        {
            string code;
            do
            {
                code = GenerateRoomCode();
            } while (_rooms.ContainsKey(code));

            var room = new GameRoom
            {
                RoomCode = code,
                HostConnectionId = hostConnectionId
            };

            room.Players.Add(new PlayerState
            {
                ConnectionId = hostConnectionId,
                Username = hostUsername
            });

            room.ActionLog.Add($"{hostUsername} creo la sala {code}.");
            _rooms[code] = room;
            return room;
        }

        public GameRoom? GetRoom(string roomCode)
        {
            _rooms.TryGetValue(roomCode.ToUpper(), out var room);
            return room;
        }

        public GameRoom? RemovePlayerByConnection(string connectionId, out string? removedUsername)
        {
            removedUsername = null;
            foreach (var room in _rooms.Values)
            {
                var player = room.Players.FirstOrDefault(p => p.ConnectionId == connectionId);
                if (player != null)
                {
                    removedUsername = player.Username;
                    room.Players.Remove(player);
                    room.ActionLog.Add($"{player.Username} salio de la sala.");

                    if (room.Players.Count == 0)
                    {
                        _rooms.TryRemove(room.RoomCode, out _);
                    }
                    return room;
                }
            }
            return null;
        }

        private static string GenerateRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            return new string(Enumerable.Repeat(chars, 4)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }
    }
}
