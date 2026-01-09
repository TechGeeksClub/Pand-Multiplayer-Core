using System;
using System.Collections.Generic;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    public sealed class LoopbackNetworkTransport : MonoBehaviour, INetworkTransport
    {
        private readonly List<PlayerInfo> players = new List<PlayerInfo>();
        private TransportConnectionState connectionState = TransportConnectionState.Disconnected;
        private TransportRoomState roomState = TransportRoomState.None;

        private string localPlayerId = string.Empty;
        private string localPlayerName = string.Empty;
        private string roomCode = string.Empty;

        public TransportConnectionState ConnectionState => connectionState;
        public TransportRoomState RoomState => roomState;

        public string LocalPlayerId => localPlayerId;
        public string LocalPlayerName => localPlayerName;

        public IReadOnlyList<PlayerInfo> Players => players;

        public event Action<TransportConnectionState> ConnectionStateChanged;
        public event Action<TransportRoomState> RoomStateChanged;
        public event Action<RoomInfo> RoomJoined;
        public event Action RoomLeft;
        public event Action<PlayerInfo> PlayerJoined;
        public event Action<PlayerInfo> PlayerLeft;
        public event Action<NetworkMessage> MessageReceived;
        public event Action<TransportError> Error;

        public void Connect(ConnectOptions options)
        {
            if (connectionState != TransportConnectionState.Disconnected)
            {
                RaiseError(TransportErrorCode.InvalidState, "Already connected/connecting.");
                return;
            }

            localPlayerId = string.IsNullOrEmpty(options?.UserId) ? Guid.NewGuid().ToString("N") : options.UserId;
            localPlayerName = string.IsNullOrEmpty(options?.PlayerName) ? "Player" : options.PlayerName;

            SetConnectionState(TransportConnectionState.Connecting);
            SetConnectionState(TransportConnectionState.Connected);
        }

        public void Disconnect()
        {
            if (connectionState == TransportConnectionState.Disconnected)
            {
                return;
            }

            if (roomState == TransportRoomState.InRoom)
            {
                LeaveRoom();
            }

            SetConnectionState(TransportConnectionState.Disconnecting);
            players.Clear();
            SetRoomState(TransportRoomState.None);
            SetConnectionState(TransportConnectionState.Disconnected);
        }

        public void CreateRoom(Pandapp.Multiplayer.Core.RoomOptions options)
        {
            EnsureConnectedOrError();
            if (connectionState != TransportConnectionState.Connected)
            {
                return;
            }

            if (string.IsNullOrEmpty(options?.RoomCode))
            {
                RaiseError(TransportErrorCode.CreateRoomFailed, "RoomCode is required.");
                return;
            }

            SetRoomState(TransportRoomState.Creating);

            roomCode = options.RoomCode;
            players.Clear();

            var localPlayer = new PlayerInfo(localPlayerId, localPlayerName)
            {
                IsHost = true,
                IsConnected = true,
            };

            players.Add(localPlayer);
            SetRoomState(TransportRoomState.InRoom);

            RoomJoined?.Invoke(BuildRoomInfo(options));
            PlayerJoined?.Invoke(localPlayer);
        }

        public void JoinRoom(string roomCode)
        {
            EnsureConnectedOrError();
            if (connectionState != TransportConnectionState.Connected)
            {
                return;
            }

            if (string.IsNullOrEmpty(roomCode))
            {
                RaiseError(TransportErrorCode.JoinRoomFailed, "RoomCode is required.");
                return;
            }

            SetRoomState(TransportRoomState.Joining);

            this.roomCode = roomCode;
            players.Clear();

            var localPlayer = new PlayerInfo(localPlayerId, localPlayerName)
            {
                IsHost = true,
                IsConnected = true,
            };

            players.Add(localPlayer);
            SetRoomState(TransportRoomState.InRoom);

            RoomJoined?.Invoke(new RoomInfo(roomCode, 4) { PlayerCount = 1 });
            PlayerJoined?.Invoke(localPlayer);
        }

        public void QuickMatch(QuickMatchOptions options)
        {
            EnsureConnectedOrError();
            if (connectionState != TransportConnectionState.Connected)
            {
                return;
            }

            var resolved = options ?? new QuickMatchOptions();
            var generatedCode = $"{resolved.RoomCodePrefix}_{Guid.NewGuid():N}".ToUpperInvariant();

            var customProperties = new Dictionary<string, object>();
            var queueId = resolved.QueueId?.Trim();
            if (!string.IsNullOrEmpty(queueId))
            {
                customProperties[MatchmakingPropertyKeys.QueueId] = queueId;
            }
            var modeId = resolved.ModeId?.Trim();
            if (!string.IsNullOrEmpty(modeId))
            {
                customProperties[MatchmakingPropertyKeys.ModeId] = modeId;
            }
            var mapId = resolved.MapId?.Trim();
            if (!string.IsNullOrEmpty(mapId))
            {
                customProperties[MatchmakingPropertyKeys.MapId] = mapId;
            }
            if (resolved.CustomProperties != null && resolved.CustomProperties.Count > 0)
            {
                foreach (var kvp in resolved.CustomProperties)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        customProperties[kvp.Key] = kvp.Value;
                    }
                }
            }

            CreateRoom(new Pandapp.Multiplayer.Core.RoomOptions
            {
                RoomCode = generatedCode,
                MaxPlayers = resolved.MaxPlayers,
                IsOpen = resolved.IsOpen,
                IsVisible = resolved.IsVisible,
                CustomProperties = customProperties,
            });
        }

        public void LeaveRoom()
        {
            if (roomState != TransportRoomState.InRoom)
            {
                return;
            }

            SetRoomState(TransportRoomState.Leaving);

            roomCode = string.Empty;

            if (players.Count > 0)
            {
                var local = players[0];
                PlayerLeft?.Invoke(local);
            }

            players.Clear();
            SetRoomState(TransportRoomState.None);
            RoomLeft?.Invoke();
        }

        public void Send(NetworkMessage message, SendOptions options)
        {
            if (roomState != TransportRoomState.InRoom)
            {
                RaiseError(TransportErrorCode.InvalidState, "Not in room.");
                return;
            }

            if (message == null)
            {
                RaiseError(TransportErrorCode.InvalidState, "Message is null.");
                return;
            }

            if (options.Target == SendTarget.Others)
            {
                return;
            }

            if (options.Target == SendTarget.Player
                && !string.IsNullOrEmpty(options.TargetPlayerId)
                && !string.Equals(options.TargetPlayerId, localPlayerId, StringComparison.Ordinal))
            {
                return;
            }

            var senderId = string.IsNullOrEmpty(message.SenderId) ? localPlayerId : message.SenderId;
            MessageReceived?.Invoke(new NetworkMessage(message.MessageId, message.Payload, senderId));
        }

        private void EnsureConnectedOrError()
        {
            if (connectionState != TransportConnectionState.Connected)
            {
                RaiseError(TransportErrorCode.InvalidState, "Not connected.");
            }
        }

        private void SetConnectionState(TransportConnectionState newState)
        {
            if (connectionState == newState)
            {
                return;
            }

            connectionState = newState;
            ConnectionStateChanged?.Invoke(newState);
        }

        private void SetRoomState(TransportRoomState newState)
        {
            if (roomState == newState)
            {
                return;
            }

            roomState = newState;
            RoomStateChanged?.Invoke(newState);
        }

        private RoomInfo BuildRoomInfo(Pandapp.Multiplayer.Core.RoomOptions options)
        {
            var info = new RoomInfo(options.RoomCode, options.MaxPlayers)
            {
                PlayerCount = players.Count,
                IsOpen = options.IsOpen,
                IsVisible = options.IsVisible,
            };

            if (options.CustomProperties != null && options.CustomProperties.Count > 0)
            {
                foreach (var kvp in options.CustomProperties)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        info.CustomProperties[kvp.Key] = kvp.Value;
                    }
                }
            }

            return info;
        }

        private void RaiseError(TransportErrorCode code, string message)
        {
            Error?.Invoke(new TransportError(code, message));
        }
    }
}
