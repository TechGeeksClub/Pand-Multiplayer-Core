using System;

namespace Pandapp.Multiplayer.Core
{
    public class NetworkMessage
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        public NetworkMessage() {}

        public NetworkMessage(int messageId, byte[] payload, string senderId = "")
        {
            MessageId = messageId;
            SenderId = senderId ?? string.Empty;
            Payload = payload ?? Array.Empty<byte>();
            TimestampUtc = DateTime.UtcNow;
        }
    }
}

