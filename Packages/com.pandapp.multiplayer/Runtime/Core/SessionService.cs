using System;
using System.Collections.Generic;
using System.Text;

namespace Pandapp.Multiplayer.Core
{
    public class SessionService : ISessionService, IDisposable
    {
        private readonly INetworkTransport transport;
        private readonly Dictionary<string, bool> readyByPlayerId = new Dictionary<string, bool>();
        private SessionState state = SessionState.Offline;
        private string roomCode = string.Empty;
        private bool disposed;

        public SessionState State => state;
        public string RoomCode => roomCode;

        public bool IsHost
        {
            get
            {
                var localPlayerId = transport.LocalPlayerId;
                if (string.IsNullOrEmpty(localPlayerId))
                {
                    return false;
                }

                var players = transport.Players;
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player != null && player.PlayerId == localPlayerId)
                    {
                        return player.IsHost;
                    }
                }

                return false;
            }
        }

        public bool LocalReady
        {
            get
            {
                var localPlayerId = transport.LocalPlayerId;
                return !string.IsNullOrEmpty(localPlayerId)
                    && readyByPlayerId.TryGetValue(localPlayerId, out var ready)
                    && ready;
            }
        }

        public IReadOnlyList<PlayerInfo> Players => transport.Players;
        public IReadOnlyDictionary<string, bool> ReadyByPlayerId => readyByPlayerId;

        public event Action<SessionState> StateChanged;
        public event Action<PlayerInfo> PlayerJoined;
        public event Action<PlayerInfo> PlayerLeft;
        public event Action<string, bool> PlayerReadyChanged;
        public event Action<SceneId> GameStartRequested;
        public event Action<TransportError> Error;

        public SessionService(INetworkTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));

            transport.ConnectionStateChanged += OnConnectionStateChanged;
            transport.RoomJoined += OnRoomJoined;
            transport.RoomLeft += OnRoomLeft;
            transport.PlayerJoined += OnTransportPlayerJoined;
            transport.PlayerLeft += OnTransportPlayerLeft;
            transport.MessageReceived += OnTransportMessageReceived;
            transport.Error += OnTransportError;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            transport.ConnectionStateChanged -= OnConnectionStateChanged;
            transport.RoomJoined -= OnRoomJoined;
            transport.RoomLeft -= OnRoomLeft;
            transport.PlayerJoined -= OnTransportPlayerJoined;
            transport.PlayerLeft -= OnTransportPlayerLeft;
            transport.MessageReceived -= OnTransportMessageReceived;
            transport.Error -= OnTransportError;
        }

        public void Connect(ConnectOptions options)
        {
            SetState(SessionState.Connecting);
            transport.Connect(options);
        }

        public void Disconnect()
        {
            transport.Disconnect();
        }

        public void CreateRoom(RoomOptions options)
        {
            SetState(SessionState.CreatingRoom);
            transport.CreateRoom(options);
        }

        public void JoinRoom(string roomCode)
        {
            SetState(SessionState.JoiningRoom);
            transport.JoinRoom(roomCode);
        }

        public void QuickMatch(QuickMatchOptions options)
        {
            SetState(SessionState.JoiningRoom);
            transport.QuickMatch(options);
        }

        public void LeaveRoom()
        {
            transport.LeaveRoom();
        }

        public void SetReady(bool ready)
        {
            if (state != SessionState.InRoom)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not in room."));
                return;
            }

            var localPlayerId = transport.LocalPlayerId;
            if (string.IsNullOrEmpty(localPlayerId))
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Local player id is missing."));
                return;
            }

            readyByPlayerId[localPlayerId] = ready;
            PlayerReadyChanged?.Invoke(localPlayerId, ready);

            var payload = new[] { ready ? (byte)1 : (byte)0 };
            var message = new NetworkMessage(CoreMessageIds.PlayerReadyChanged, payload, localPlayerId);
            transport.Send(message, SendOptions.ToOthers());
        }

        public void RequestStart(SceneId sceneId)
        {
            if (state != SessionState.InRoom)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not in room."));
                return;
            }

            if (!IsHost)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Only host can start the game."));
                return;
            }

            SetState(SessionState.StartingGame);

            var sceneValue = sceneId.Value ?? string.Empty;
            var payload = Encoding.UTF8.GetBytes(sceneValue);
            var message = new NetworkMessage(CoreMessageIds.GameStartRequested, payload, transport.LocalPlayerId);
            transport.Send(message, SendOptions.ToOthers());

            GameStartRequested?.Invoke(sceneId);
        }

        public void Send(NetworkMessage message, SendOptions options)
        {
            transport.Send(message, options);
        }

        private void OnConnectionStateChanged(TransportConnectionState connectionState)
        {
            switch (connectionState)
            {
                case TransportConnectionState.Connecting:
                    SetState(SessionState.Connecting);
                    break;
                case TransportConnectionState.Connected:
                    if (state != SessionState.InRoom && state != SessionState.StartingGame && state != SessionState.InGame)
                    {
                        SetState(SessionState.Online);
                    }
                    break;
                case TransportConnectionState.Disconnected:
                    roomCode = string.Empty;
                    readyByPlayerId.Clear();
                    SetState(SessionState.Offline);
                    break;
            }
        }

        private void OnRoomJoined(RoomInfo room)
        {
            roomCode = room?.RoomCode ?? string.Empty;
            RebuildReadyFromPlayers();
            SetState(SessionState.InRoom);
        }

        private void OnRoomLeft()
        {
            roomCode = string.Empty;
            readyByPlayerId.Clear();

            SetState(transport.ConnectionState == TransportConnectionState.Connected
                ? SessionState.Online
                : SessionState.Offline);
        }

        private void OnTransportPlayerJoined(PlayerInfo player)
        {
            if (player != null && !string.IsNullOrEmpty(player.PlayerId) && !readyByPlayerId.ContainsKey(player.PlayerId))
            {
                readyByPlayerId[player.PlayerId] = false;
            }

            PlayerJoined?.Invoke(player);
        }

        private void OnTransportPlayerLeft(PlayerInfo player)
        {
            if (player != null && !string.IsNullOrEmpty(player.PlayerId))
            {
                readyByPlayerId.Remove(player.PlayerId);
            }

            PlayerLeft?.Invoke(player);
        }

        private void OnTransportMessageReceived(NetworkMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (state != SessionState.InRoom && state != SessionState.StartingGame && state != SessionState.InGame)
            {
                return;
            }

            switch (message.MessageId)
            {
                case CoreMessageIds.PlayerReadyChanged:
                    HandleRemoteReadyChanged(message);
                    break;
                case CoreMessageIds.GameStartRequested:
                    HandleRemoteStartRequested(message);
                    break;
            }
        }

        private void HandleRemoteReadyChanged(NetworkMessage message)
        {
            if (message.Payload == null || message.Payload.Length == 0)
            {
                return;
            }

            var senderId = message.SenderId;
            if (string.IsNullOrEmpty(senderId))
            {
                return;
            }

            var ready = message.Payload[0] == 1;
            readyByPlayerId[senderId] = ready;
            PlayerReadyChanged?.Invoke(senderId, ready);
        }

        private void HandleRemoteStartRequested(NetworkMessage message)
        {
            var sceneValue = message.Payload == null || message.Payload.Length == 0
                ? string.Empty
                : Encoding.UTF8.GetString(message.Payload);

            var sceneId = new SceneId(sceneValue);
            SetState(SessionState.StartingGame);
            GameStartRequested?.Invoke(sceneId);
        }

        private void OnTransportError(TransportError error)
        {
            RaiseError(error);
        }

        private void RaiseError(TransportError error)
        {
            Error?.Invoke(error);
        }

        private void SetState(SessionState newState)
        {
            if (state == newState)
            {
                return;
            }

            state = newState;
            StateChanged?.Invoke(newState);
        }

        private void RebuildReadyFromPlayers()
        {
            readyByPlayerId.Clear();

            var players = transport.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }

                readyByPlayerId[player.PlayerId] = player.IsReady;
            }
        }
    }
}
