using System.Collections.Generic;

namespace Pandapp.Multiplayer.Core
{
    public class PlayerInfo
    {
        public string PlayerId { get; set; }
        public string PlayerName { get; set; }
        public bool IsHost { get; set; }
        public bool IsReady { get; set; }
        public bool IsConnected { get; set; }
        public Dictionary<string, object> CustomData { get; set; }
        
        public PlayerInfo()
        {
            CustomData = new Dictionary<string, object>();
        }
        
        public PlayerInfo(string playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
            IsHost = false;
            IsReady = false;
            IsConnected = true;
            CustomData = new Dictionary<string, object>();
        }
    }
}

