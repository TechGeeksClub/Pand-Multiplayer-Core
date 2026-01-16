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
        [Header("Pun2 Tuning")]
        [Tooltip("If enabled, sets Photon send/serialization rates on Connect for smoother RaiseEvent traffic.")]
        [SerializeField] private bool applyPhotonRatesOnConnect = true;
        [Min(10)]
        [SerializeField] private int photonSendRate = 60;
        [Min(10)]
        [SerializeField] private int photonSerializationRate = 60;

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
                if (localPlayer == null || localPlayer.ActorNumber <= 0)
                {
                    return string.Empty;
                }

                return localPlayer.ActorNumber.ToString();
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

        public override void OnEnable()
        {
            base.OnEnable();
            PhotonNetwork.AddCallbackTarget(this);
        }

        public override void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            base.OnDisable();
        }

        public void Connect(ConnectOptions options)
        {
            if (connectionState != TransportConnectionState.Disconnected)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "Already connecting/connected."));
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = true;

            ApplyPhotonRates();

            var playerName = options?.PlayerName;
            PhotonNetwork.NickName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;

            if (!string.IsNullOrEmpty(options?.UserId))
            {
                PhotonNetwork.AuthValues = new AuthenticationValues(options.UserId);
            }

            if (!string.IsNullOrEmpty(options?.GameVersion))
            {
                PhotonNetwork.GameVersion = options.GameVersion;
            }

            SetConnectionState(TransportConnectionState.Connecting);

            if (!PhotonNetwork.ConnectUsingSettings())
            {
                SetConnectionState(TransportConnectionState.Disconnected);
                RaiseError(new TransportError(TransportErrorCode.ConnectFailed, "ConnectUsingSettings returned false."));
            }
        }

        private void ApplyPhotonRates()
        {
            if (!applyPhotonRatesOnConnect)
            {
                return;
            }

            if (photonSendRate > 0)
            {
                PhotonNetwork.SendRate = photonSendRate;
            }

            if (photonSerializationRate > 0)
            {
                PhotonNetwork.SerializationRate = photonSerializationRate;
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

            ApplyCustomProperties(photonOptions, options.CustomProperties);
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
            var resolved = pendingQuickMatch ?? new QuickMatchOptions();
            var matchProperties = BuildMatchProperties(resolved);
            var expectedMaxPlayers = resolved.MaxPlayers;

            bool joinResult;
            if (matchProperties.Count == 0)
            {
                joinResult = expectedMaxPlayers == 0
                    ? PhotonNetwork.JoinRandomRoom()
                    : PhotonNetwork.JoinRandomRoom(null, expectedMaxPlayers);
            }
            else
            {
                var expectedProperties = BuildPhotonHashtable(matchProperties);
                joinResult = PhotonNetwork.JoinRandomRoom(expectedProperties, expectedMaxPlayers);
            }

            if (!joinResult)
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

            if (message.MessageId < 0 || message.MessageId >= 200)
            {
                RaiseError(new TransportError(TransportErrorCode.InvalidState, "MessageId must be between 0 and 199 for Photon PUN2 (200+ is reserved)."));
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

        private static Dictionary<string, object> BuildMatchProperties(QuickMatchOptions options)
        {
            var properties = new Dictionary<string, object>();

            if (options == null)
            {
                return properties;
            }

            var queueId = options.QueueId?.Trim();
            if (!string.IsNullOrEmpty(queueId))
            {
                properties[MatchmakingPropertyKeys.QueueId] = queueId;
            }

            var modeId = options.ModeId?.Trim();
            if (!string.IsNullOrEmpty(modeId))
            {
                properties[MatchmakingPropertyKeys.ModeId] = modeId;
            }

            var mapId = options.MapId?.Trim();
            if (!string.IsNullOrEmpty(mapId))
            {
                properties[MatchmakingPropertyKeys.MapId] = mapId;
            }

            if (options.CustomProperties != null && options.CustomProperties.Count > 0)
            {
                foreach (var kvp in options.CustomProperties)
                {
                    if (string.IsNullOrEmpty(kvp.Key))
                    {
                        continue;
                    }

                    properties[kvp.Key] = kvp.Value;
                }
            }

            return properties;
        }

        private static void ApplyCustomProperties(Photon.Realtime.RoomOptions roomOptions, IReadOnlyDictionary<string, object> customProperties)
        {
            if (roomOptions == null || customProperties == null || customProperties.Count == 0)
            {
                return;
            }

            var hashtable = BuildPhotonHashtable(customProperties);
            if (hashtable.Count == 0)
            {
                return;
            }

            roomOptions.CustomRoomProperties = hashtable;

            var keys = BuildLobbyPropertyKeys(customProperties);
            if (keys.Length > 0)
            {
                roomOptions.CustomRoomPropertiesForLobby = keys;
            }
        }

        private static Hashtable BuildPhotonHashtable(IReadOnlyDictionary<string, object> properties)
        {
            var hashtable = new Hashtable();

            if (properties == null)
            {
                return hashtable;
            }

            foreach (var kvp in properties)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    continue;
                }

                hashtable[kvp.Key] = kvp.Value;
            }

            return hashtable;
        }

        private static string[] BuildLobbyPropertyKeys(IReadOnlyDictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                return Array.Empty<string>();
            }

            var keys = new List<string>(properties.Count);
            foreach (var key in properties.Keys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    keys.Add(key);
                }
            }

            return keys.Count == 0 ? Array.Empty<string>() : keys.ToArray();
        }

        private static void CopyPhotonHashtable(Hashtable source, Dictionary<string, object> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            foreach (System.Collections.DictionaryEntry entry in source)
            {
                if (entry.Key is string key && !string.IsNullOrEmpty(key))
                {
                    destination[key] = entry.Value;
                }
            }
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

            var matchProperties = BuildMatchProperties(resolved);
            ApplyCustomProperties(photonOptions, matchProperties);

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

            if (photonEvent.Code >= 200)
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
            if (room == null)
            {
                return new CoreRoomInfo();
            }

            var info = new CoreRoomInfo
            {
                RoomCode = room.Name ?? string.Empty,
                MaxPlayers = (byte)room.MaxPlayers,
                PlayerCount = room.PlayerCount,
                IsOpen = room.IsOpen,
                IsVisible = room.IsVisible,
            };

            CopyPhotonHashtable(room.CustomProperties, info.CustomProperties);
            return info;
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
                PlayerId = photonPlayer?.ActorNumber.ToString() ?? string.Empty,
                PlayerName = photonPlayer?.NickName ?? string.Empty,
                IsHost = photonPlayer != null && photonPlayer.IsMasterClient,
                IsConnected = true,
            };

            info.CustomData["ActorNumber"] = photonPlayer?.ActorNumber ?? -1;
            info.CustomData["UserId"] = photonPlayer?.UserId ?? string.Empty;
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
            return senderActorNumber.ToString();
        }
    }
}
#endif
