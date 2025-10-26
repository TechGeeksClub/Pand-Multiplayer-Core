using System;

namespace PandNet.Core
{
    public class GameMessage
    {
        public string MessageType { get; set; }
        public string SenderId { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
        
        public GameMessage()
        {
            Timestamp = DateTime.Now;
        }
        
        public GameMessage(string messageType, string senderId, object data)
        {
            MessageType = messageType;
            SenderId = senderId;
            Data = data;
            Timestamp = DateTime.Now;
        }
    }
}

