using System.Collections.Generic;

namespace Pandapp.Multiplayer.Core
{
    public class ConnectOptions
    {
        public string PlayerName { get; set; } = "Player";
        public string UserId { get; set; } = string.Empty;
        public string GameVersion { get; set; } = string.Empty;
    }

    public class RoomOptions
    {
        public string RoomCode { get; set; } = string.Empty;
        public byte MaxPlayers { get; set; } = 4;
        public bool IsVisible { get; set; } = true;
        public bool IsOpen { get; set; } = true;
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }

    public class QuickMatchOptions
    {
        public byte MaxPlayers { get; set; } = 4;
        public bool IsVisible { get; set; } = true;
        public bool IsOpen { get; set; } = true;
        public string RoomCodePrefix { get; set; } = "QM";
        public string QueueId { get; set; } = string.Empty;
        public string ModeId { get; set; } = string.Empty;
        public string MapId { get; set; } = string.Empty;
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }
}
