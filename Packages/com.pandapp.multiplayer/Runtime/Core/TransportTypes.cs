using System;

namespace Pandapp.Multiplayer.Core
{
    public enum TransportConnectionState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3,
    }

    public enum TransportRoomState
    {
        None = 0,
        Creating = 1,
        Joining = 2,
        InRoom = 3,
        Leaving = 4,
    }

    public enum SendTarget
    {
        All = 0,
        Others = 1,
        Host = 2,
        Player = 3,
    }

    public struct SendOptions
    {
        public SendTarget Target;
        public bool Reliable;
        public string TargetPlayerId;

        public SendOptions(SendTarget target, bool reliable = true, string targetPlayerId = null)
        {
            Target = target;
            Reliable = reliable;
            TargetPlayerId = targetPlayerId;
        }

        public static SendOptions ToAll(bool reliable = true) => new SendOptions(SendTarget.All, reliable);
        public static SendOptions ToOthers(bool reliable = true) => new SendOptions(SendTarget.Others, reliable);
        public static SendOptions ToHost(bool reliable = true) => new SendOptions(SendTarget.Host, reliable);
        public static SendOptions ToPlayer(string playerId, bool reliable = true) => new SendOptions(SendTarget.Player, reliable, playerId);
    }

    public enum TransportErrorCode
    {
        Unknown = 0,
        InvalidState = 1,
        ConnectFailed = 2,
        DisconnectFailed = 3,
        CreateRoomFailed = 4,
        JoinRoomFailed = 5,
        LeaveRoomFailed = 6,
        SendFailed = 7,
    }

    public struct TransportError
    {
        public TransportErrorCode Code;
        public string Message;

        public TransportError(TransportErrorCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Message) ? Code.ToString() : $"{Code}: {Message}";
        }
    }
}
