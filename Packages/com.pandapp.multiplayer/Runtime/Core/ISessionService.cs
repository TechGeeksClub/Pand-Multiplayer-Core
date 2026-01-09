using System;
using System.Collections.Generic;

namespace Pandapp.Multiplayer.Core
{
    public interface ISessionService
    {
        SessionState State { get; }
        string RoomCode { get; }

        bool IsHost { get; }
        bool LocalReady { get; }

        IReadOnlyList<PlayerInfo> Players { get; }
        IReadOnlyDictionary<string, bool> ReadyByPlayerId { get; }

        event Action<SessionState> StateChanged;
        event Action<PlayerInfo> PlayerJoined;
        event Action<PlayerInfo> PlayerLeft;
        event Action<string, bool> PlayerReadyChanged;
        event Action<SceneId> GameStartRequested;
        event Action<TransportError> Error;

        void Connect(ConnectOptions options);
        void Disconnect();

        void CreateRoom(RoomOptions options);
        void JoinRoom(string roomCode);
        void QuickMatch(QuickMatchOptions options);
        void LeaveRoom();

        void SetReady(bool ready);
        void RequestStart(SceneId sceneId);

        void Send(NetworkMessage message, SendOptions options);
    }
}
