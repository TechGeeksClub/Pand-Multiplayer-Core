using System;
using System.Collections.Generic;

namespace Pandapp.Multiplayer.Core
{
    public interface INetworkTransport
    {
        TransportConnectionState ConnectionState { get; }
        TransportRoomState RoomState { get; }

        string LocalPlayerId { get; }
        string LocalPlayerName { get; }

        IReadOnlyList<PlayerInfo> Players { get; }

        event Action<TransportConnectionState> ConnectionStateChanged;
        event Action<TransportRoomState> RoomStateChanged;

        event Action<RoomInfo> RoomJoined;
        event Action RoomLeft;

        event Action<PlayerInfo> PlayerJoined;
        event Action<PlayerInfo> PlayerLeft;

        event Action<NetworkMessage> MessageReceived;
        event Action<TransportError> Error;

        void Connect(ConnectOptions options);
        void Disconnect();

        void CreateRoom(RoomOptions options);
        void JoinRoom(string roomCode);
        void QuickMatch(QuickMatchOptions options);
        void LeaveRoom();

        void Send(NetworkMessage message, SendOptions options);
    }
}
