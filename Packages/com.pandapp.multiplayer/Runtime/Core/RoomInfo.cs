using System.Collections.Generic;

namespace Pandapp.Multiplayer.Core
{
    public class RoomInfo
    {
        public string RoomCode { get; set; } = string.Empty;
        public byte MaxPlayers { get; set; } = 4;
        public int PlayerCount { get; set; }
        public bool IsOpen { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();

        public RoomInfo() {}

        public RoomInfo(string roomCode, byte maxPlayers)
        {
            RoomCode = roomCode;
            MaxPlayers = maxPlayers;
        }
    }
}

