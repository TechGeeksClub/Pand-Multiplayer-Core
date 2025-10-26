using System;
using System.Collections.Generic;

namespace PandNet.Core
{
    public class GameInfo
    {
        public string GameId { get; set; }
        public string GameName { get; set; }
        public int MaxPlayers { get; set; }
        public int CurrentPlayers { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, string> CustomProperties { get; set; }
        
        public GameInfo()
        {
            CustomProperties = new Dictionary<string, string>();
        }
        
        public GameInfo(string gameId, string gameName, int maxPlayers)
        {
            GameId = gameId;
            GameName = gameName;
            MaxPlayers = maxPlayers;
            CurrentPlayers = 0;
            IsActive = true;
            CustomProperties = new Dictionary<string, string>();
        }
    }
}

