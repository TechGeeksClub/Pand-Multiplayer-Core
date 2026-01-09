#if PHOTON_UNITY_NETWORKING && PANDAPP_PHOTON_PUN2
using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Pandapp.Multiplayer.Core;
using CoreRoomInfo = Pandapp.Multiplayer.Core.RoomInfo;
using PhotonSendOptions = ExitGames.Client.Photon.SendOptions;

namespace Pandapp.Multiplayer.Transport.Pun2
{
    public class Pun2NetworkTransport : MonoBehaviourPunCallbacks, IOnEventCallback, INetworkTransport
    {
        private readonly List<PlayerInfo> players = new List<PlayerInfo>();
        private TransportConnectionState connectionState = TransportConnectionState.Disconnected;
        private TransportRoomState roomState = TransportRoomState.None;
        private QuickMatchOptions pendingQuickMatch;
        private bool quickMatchActive;
        private bool quickMatchWaitingLobby;

        public TransportConnectionState ConnectionState => connectionState;
        public TransportRoomState RoomState => roomState;

        public string LocalPlayerId
        {
            get
            {
                var localPlayer = PhotonNetwork.LocalPlayer;
                return localPlayer == null
                    ? string.Empty
                    : (localPlayer.UserId ?? localPlayer.ActorNumber.ToString());
            }
        }

        public string LocalPlayerName => PhotonNetwork.NickName ?? string.Empty;

        public IReadOnlyList<PlayerInfo> Players => players;

        public event Action<TransportConnectionState> ConnectionStateChanged;
        public event Action<TransportRoomState> RoomStateChanged;
        public event Action<CoreRoomInfo> RoomJoined;
        public event Action RoomLeft;
        public event Action<PlayerInfo> PlayerJoined;
        public event Action<PlayerInfo> PlayerLeft;
        public event Action<NetworkMessage> MessageReceived;
        public event Action<TransportError> Error;

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void Connect(ConnectOptions options)
        {
            if (connectionState != TransportConnectionState.Disconnected)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Already connecting/connected."));
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = true;

            var playerName = options?.PlayerName;
            PhotonNetwork.NickName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;

            if (!string.IsNullOrEmpty(options?.UserId))
            {
                PhotonNetwork.AuthValues = new AuthenticationValues(options.UserId);
            }

            SetConnectionState(TransportConnectionState.Connecting);

            if (!PhotonNetwork.ConnectUsingSettings())
            {
                SetConnectionState(TransportConnectionState.Disconnected);
                RaiseError(new TransportError(TransportErrorCode.ConnectFailed, "ConnectUsingSettings returned false."));
            }
        }

        public void Disconnect()
        {
            if (connectionState == TransportConnectionState.Disconnected)
            {
                return;
            }

            SetConnectionState(TransportConnectionState.Disconnecting);
            PhotonNetwork.Disconnect();
        }

        public void CreateRoom(Pandapp.Multiplayer.Core.RoomOptions options)
        {
            if (connectionState != TransportConnectionState.Connected)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not connected."));
                return;
            }

            if (options == null || string.IsNullOrEmpty(options.RoomCode))
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "RoomCode is required."));
                return;
            }

            SetRoomState(TransportRoomState.Creating);

            var photonOptions = new Photon.Realtime.RoomOptions
            {
                MaxPlayers = options.MaxPlayers,
                IsVisible = options.IsVisible,
                IsOpen = options.IsOpen,
            };

            PhotonNetwork.CreateRoom(options.RoomCode, photonOptions);
        }

        public void JoinRoom(string roomCode)
        {
            if (connectionState != TransportConnectionState.Connected)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not connected."));
                return;
            }

            if (string.IsNullOrEmpty(roomCode))
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "RoomCode is required."));
                return;
            }

            SetRoomState(TransportRoomState.Joining);
            PhotonNetwork.JoinRoom(roomCode);
        }

        public void QuickMatch(QuickMatchOptions options)
        {
            if (connectionState != TransportConnectionState.Connected)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not connected."));
                return;
            }

            if (IsInPhotonRoom())
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Already in room."));
                return;
            }

            pendingQuickMatch = options ?? new QuickMatchOptions();
            quickMatchActive = true;
            quickMatchWaitingLobby = false;

            SetRoomState(TransportRoomState.Joining);

            if (!PhotonNetwork.InLobby)
            {
                if (PhotonNetwork.JoinLobby())
                {
                    quickMatchWaitingLobby = true;
                    return;
                }

                quickMatchWaitingLobby = false;
            }

            TryJoinRandomRoomOrCreate();
        }

        private void TryJoinRandomRoomOrCreate()
        {
            if (!PhotonNetwork.JoinRandomRoom())
            {
                CreateQuickMatchRoom();
            }
        }

        public void LeaveRoom()
        {
            if (!IsInPhotonRoom())
            {
                return;
            }

            if (roomState != TransportRoomState.InRoom)
            {
                SetRoomState(TransportRoomState.InRoom);
            }

            SetRoomState(TransportRoomState.Leaving);
            PhotonNetwork.LeaveRoom();
        }

        public void Send(NetworkMessage message, Pandapp.Multiplayer.Core.SendOptions options)
        {
            if (!IsInPhotonRoom())
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Not in room."));
                return;
            }

            if (roomState != TransportRoomState.InRoom)
            {
                SetRoomState(TransportRoomState.InRoom);
            }

            if (message == null)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Message is null."));
                return;
            }

            if (message.MessageId < byte.MinValue || message.MessageId > byte.MaxValue)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "MessageId must fit into a byte for PUN2 RaiseEvent."));
                return;
            }

            var eventCode = (byte)message.MessageId;
            var raiseOptions = BuildRaiseEventOptions(options);

            var photonSendOptions = new PhotonSendOptions { Reliability = options.Reliable };
            PhotonNetwork.RaiseEvent(eventCode, message.Payload ?? Array.Empty<byte>(), raiseOptions, photonSendOptions);
        }

        private static bool IsInPhotonRoom()
        {
            return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
        }

        private void CreateQuickMatchRoom()
        {
            var resolved = pendingQuickMatch ?? new QuickMatchOptions();
            var roomCode = BuildQuickMatchRoomCode(resolved);

            SetRoomState(TransportRoomState.Creating);

            var photonOptions = new Photon.Realtime.RoomOptions
            {
                MaxPlayers = resolved.MaxPlayers,
                IsVisible = resolved.IsVisible,
                IsOpen = resolved.IsOpen,
            };

            PhotonNetwork.CreateRoom(roomCode, photonOptions);
        }

        private static string BuildQuickMatchRoomCode(QuickMatchOptions options)
        {
            var prefix = options?.RoomCodePrefix ?? "QM";
            prefix = prefix.Trim();
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "QM";
            }

            return $"{prefix}_{Guid.NewGuid():N}".ToUpperInvariant();
        }

        public override void OnConnectedToMaster()
        {
            SetConnectionState(TransportConnectionState.Connected);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            quickMatchActive = false;
            pendingQuickMatch = null;
            quickMatchWaitingLobby = false;
            players.Clear();
            SetRoomState(TransportRoomState.None);
            SetConnectionState(TransportConnectionState.Disconnected);
        }

        public override void OnJoinedRoom()
        {
            quickMatchActive = false;
            quickMatchWaitingLobby = false;
            RefreshPlayers();
            SetRoomState(TransportRoomState.InRoom);
            RoomJoined?.Invoke(BuildRoomInfo());
        }

        public override void OnJoinedLobby()
        {
            if (!quickMatchActive || !quickMatchWaitingLobby)
            {
                return;
            }

            quickMatchWaitingLobby = false;
            TryJoinRandomRoomOrCreate();
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (!quickMatchActive)
            {
                SetRoomState(TransportRoomState.None);
                RaiseError(new TransportError(TransportErrorCode.JoinRoomFailed, $"{returnCode}: {message}"));
                return;
            }

            CreateQuickMatchRoom();
        }

        public override void OnLeftRoom()
        {
            quickMatchActive = false;
            pendingQuickMatch = null;
            quickMatchWaitingLobby = false;
            players.Clear();
            SetRoomState(TransportRoomState.None);
            RoomLeft?.Invoke();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            quickMatchActive = false;
            pendingQuickMatch = null;
            quickMatchWaitingLobby = false;
            SetRoomState(TransportRoomState.None);
            RaiseError(new TransportError(TransportErrorCode.JoinRoomFailed, $"{returnCode}: {message}"));
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            quickMatchActive = false;
            pendingQuickMatch = null;
            quickMatchWaitingLobby = false;
            SetRoomState(TransportRoomState.None);
            RaiseError(new TransportError(TransportErrorCode.CreateRoomFailed, $"{returnCode}: {message}"));
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            var info = ToPlayerInfo(newPlayer);
            players.Add(info);
            RefreshHostFlags();
            PlayerJoined?.Invoke(info);
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            var removed = RemovePlayer(otherPlayer);
            RefreshHostFlags();
            if (removed != null)
            {
                PlayerLeft?.Invoke(removed);
            }
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            RefreshHostFlags();
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null)
            {
                return;
            }

            if (roomState != TransportRoomState.InRoom)
            {
                return;
            }

            if (!(photonEvent.CustomData is byte[] payload))
            {
                return;
            }

            var senderId = ResolveSenderId(photonEvent.Sender);
            var message = new NetworkMessage(photonEvent.Code, payload, senderId);
            MessageReceived?.Invoke(message);
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

        private void RaiseError(TransportError error)
        {
            Error?.Invoke(error);
        }

        private CoreRoomInfo BuildRoomInfo()
        {
            var room = PhotonNetwork.CurrentRoom;
            return room == null
                ? new CoreRoomInfo()
                : new CoreRoomInfo
                {
                    RoomCode = room.Name ?? string.Empty,
                    MaxPlayers = (byte)room.MaxPlayers,
                    PlayerCount = room.PlayerCount,
                    IsOpen = room.IsOpen,
                    IsVisible = room.IsVisible,
                };
        }

        private void RefreshPlayers()
        {
            players.Clear();

            var photonPlayers = PhotonNetwork.PlayerList;
            for (var i = 0; i < photonPlayers.Length; i++)
            {
                players.Add(ToPlayerInfo(photonPlayers[i]));
            }

            RefreshHostFlags();
        }

        private void RefreshHostFlags()
        {
            var master = PhotonNetwork.MasterClient;
            var masterActorNumber = master?.ActorNumber ?? -1;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.CustomData != null
                    && player.CustomData.TryGetValue("ActorNumber", out var actorNumberObj)
                    && actorNumberObj is int actorNumber)
                {
                    player.IsHost = actorNumber == masterActorNumber;
                }
            }
        }

        private PlayerInfo RemovePlayer(Player otherPlayer)
        {
            if (otherPlayer == null)
            {
                return null;
            }

            var actorNumber = otherPlayer.ActorNumber;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.CustomData == null)
                {
                    continue;
                }

                if (player.CustomData.TryGetValue("ActorNumber", out var actorNumberObj)
                    && actorNumberObj is int existingActorNumber
                    && existingActorNumber == actorNumber)
                {
                    players.RemoveAt(i);
                    return player;
                }
            }

            return null;
        }

        private static PlayerInfo ToPlayerInfo(Player photonPlayer)
        {
            var info = new PlayerInfo
            {
                PlayerId = photonPlayer?.UserId ?? photonPlayer?.ActorNumber.ToString() ?? string.Empty,
                PlayerName = photonPlayer?.NickName ?? string.Empty,
                IsHost = photonPlayer != null && photonPlayer.IsMasterClient,
                IsConnected = true,
            };

            info.CustomData["ActorNumber"] = photonPlayer?.ActorNumber ?? -1;
            return info;
        }

        private static RaiseEventOptions BuildRaiseEventOptions(Pandapp.Multiplayer.Core.SendOptions options)
        {
            var raiseOptions = new RaiseEventOptions();

            switch (options.Target)
            {
                case SendTarget.All:
                    raiseOptions.Receivers = ReceiverGroup.All;
                    break;
                case SendTarget.Others:
                    raiseOptions.Receivers = ReceiverGroup.Others;
                    break;
                case SendTarget.Host:
                {
                    var master = PhotonNetwork.MasterClient;
                    if (master != null)
                    {
                        raiseOptions.TargetActors = new[] { master.ActorNumber };
                    }
                    break;
                }
                case SendTarget.Player:
                {
                    var targetActor = ResolveActorNumber(options.TargetPlayerId);
                    if (targetActor > 0)
                    {
                        raiseOptions.TargetActors = new[] { targetActor };
                    }
                    break;
                }
            }

            return raiseOptions;
        }

        private static int ResolveActorNumber(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || PhotonNetwork.CurrentRoom == null)
            {
                return -1;
            }

            if (int.TryParse(playerId, out var actorNumber))
            {
                return actorNumber;
            }

            foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
            {
                var player = kvp.Value;
                if (player != null && player.UserId == playerId)
                {
                    return player.ActorNumber;
                }
            }

            return -1;
        }

        private static string ResolveSenderId(int senderActorNumber)
        {
            if (PhotonNetwork.CurrentRoom == null)
            {
                return senderActorNumber.ToString();
            }

            if (PhotonNetwork.CurrentRoom.Players != null
                && PhotonNetwork.CurrentRoom.Players.TryGetValue(senderActorNumber, out var sender))
            {
                return sender.UserId ?? sender.ActorNumber.ToString();
            }

            return senderActorNumber.ToString();
        }
    }
}
#endif
