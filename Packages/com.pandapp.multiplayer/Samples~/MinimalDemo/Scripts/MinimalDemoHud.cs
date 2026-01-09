using System;
using System.Text;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using Pandapp.Multiplayer.Gameplay;
using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    public sealed class MinimalDemoHud : MonoBehaviour
    {
        private const int PlayerPrefabId = 1;
        private const int BallPrefabId = 2;

        [Header("Defaults")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string userId = string.Empty;
        [SerializeField] private string gameVersion = "dev";
        [SerializeField] private string roomCode = "ROOM";
        [SerializeField] private byte maxPlayers = 4;
        [SerializeField] private string queueId = "default";
        [SerializeField] private string modeId = "1v1";
        [SerializeField] private string mapId = "default";
        [SerializeField] private string gameSceneName = "PandappMinimalDemo_Game";

        [Header("Message")]
        [SerializeField] private int messageId = 100;
        [SerializeField] private string messageText = "hello";

        [Header("Bindings")]
        [SerializeField] private MinimalDemoGameModule gameModule;

        private NetworkSpawner cachedSpawner;
        private NetworkPrefabCatalog runtimeCatalog;
        private GameObject playerTemplate;
        private GameObject ballTemplate;

        private Vector2 scroll;

        private void Awake()
        {
            if (gameModule == null)
            {
                gameModule = GetComponent<MinimalDemoGameModule>();
            }
        }

        private void OnGUI()
        {
            var app = MultiplayerApp.Instance;
            if (app == null)
            {
                GUILayout.Label("MultiplayerApp not found in scene.");
                return;
            }

            var session = app.Session;
            var transport = app.Transport;

            GUILayout.BeginArea(new Rect(12, 12, 520, Screen.height - 24), GUI.skin.box);

            GUILayout.Label("Pandapp Multiplayer - Minimal Demo");

            GUILayout.Space(8);
            GUILayout.Label($"Transport: {(transport != null ? transport.GetType().Name : "<null>")}");
            GUILayout.Label($"Connection: {(transport != null ? transport.ConnectionState.ToString() : "<null>")}");
            GUILayout.Label($"Room: {(transport != null ? transport.RoomState.ToString() : "<null>")}");
            GUILayout.Label($"Session: {(session != null ? session.State.ToString() : "<null>")}");
            GUILayout.Label($"IsHost: {(session != null && session.IsHost)} | Ready: {(session != null && session.LocalReady)} | RoomCode: {(session != null ? session.RoomCode : "")}");
#if PHOTON_UNITY_NETWORKING
            var cloudRegion = PhotonNetwork.NetworkingClient != null ? PhotonNetwork.NetworkingClient.CloudRegion : string.Empty;
            var fixedRegion = PhotonNetwork.PhotonServerSettings != null
                ? PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion
                : string.Empty;

            GUILayout.Label($"Photon: {PhotonNetwork.NetworkClientState} | ConnectedReady: {PhotonNetwork.IsConnectedAndReady} | InLobby: {PhotonNetwork.InLobby}");
            GUILayout.Label($"Photon Region: {(string.IsNullOrEmpty(cloudRegion) ? "<empty>" : cloudRegion)} | FixedRegion: {(string.IsNullOrEmpty(fixedRegion) ? "<empty>" : fixedRegion)}");
            GUILayout.Label($"Photon Server: {PhotonNetwork.ServerAddress} | GameVersion: {PhotonNetwork.GameVersion}");
#endif

            GUILayout.Space(12);
            GUILayout.Label("Player");
            playerName = LabeledTextField("Name", playerName);
            userId = LabeledTextField("UserId (optional)", userId);
            gameVersion = LabeledTextField("GameVersion", gameVersion);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect", GUILayout.Height(28)))
            {
                app.Connect(new ConnectOptions { PlayerName = playerName, UserId = userId, GameVersion = gameVersion });
            }
            if (GUILayout.Button("Disconnect", GUILayout.Height(28)))
            {
                app.Disconnect();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("Room");
            roomCode = LabeledTextField("Room Code", roomCode);
            maxPlayers = (byte)Mathf.Clamp(LabeledIntField("Max Players", maxPlayers), 1, 16);
            queueId = LabeledTextField("Queue Id", queueId);
            modeId = LabeledTextField("Mode Id", modeId);
            mapId = LabeledTextField("Map Id", mapId);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create", GUILayout.Height(28)))
            {
                app.CreateRoom(new RoomOptions
                {
                    RoomCode = roomCode,
                    MaxPlayers = maxPlayers,
                    IsOpen = true,
                    IsVisible = true,
                });
            }
            if (GUILayout.Button("Join", GUILayout.Height(28)))
            {
                app.JoinRoom(roomCode);
            }
            if (GUILayout.Button("Quick Match", GUILayout.Height(28)))
            {
                app.QuickMatch(new QuickMatchOptions
                {
                    MaxPlayers = maxPlayers,
                    IsOpen = true,
                    IsVisible = true,
                    RoomCodePrefix = "QM",
                    QueueId = queueId,
                    ModeId = modeId,
                    MapId = mapId,
                });
            }
            if (GUILayout.Button("Leave", GUILayout.Height(28)))
            {
                app.LeaveRoom();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle Ready", GUILayout.Height(28)))
            {
                app.SetReady(session == null || !session.LocalReady);
            }
            gameSceneName = LabeledTextField("Game Scene Name", gameSceneName);
            var canStart = session != null
                && session.State == SessionState.InRoom
                && session.IsHost;

            var previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canStart;
            if (GUILayout.Button("Start", GUILayout.Height(28)))
            {
                app.RequestStart(new SceneId(gameSceneName));
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("Gameplay (Spawn + Authority)");
            GUILayout.Label("WASD: move your player | Arrow keys: move ball (host only)");

            var spawner = GetOrCreateSpawner(app);
            GUILayout.Label($"Spawner: {(spawner != null ? spawner.GetType().Name : "<null>")} | Spawned: {(spawner != null ? spawner.SpawnCount.ToString() : "0")}");

            var canSpawn = transport != null
                && transport.RoomState == TransportRoomState.InRoom
                && session != null
                && session.IsHost;

            previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && canSpawn;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn Players + Ball", GUILayout.Height(28)))
            {
                SpawnPlayersAndBall(app, spawner);
            }
            if (GUILayout.Button("Despawn All", GUILayout.Height(28)))
            {
                spawner?.TryDespawnAllForAll();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = previousEnabled;

            GUILayout.Space(12);
            GUILayout.Label("Message");
            messageId = LabeledIntField("MessageId (>= 100)", messageId);
            messageText = LabeledTextField("Text", messageText);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Send", GUILayout.Height(28)))
            {
                if (messageId < 100)
                {
                    Debug.LogError("MessageId must be >= 100 (1-99 reserved for core).");
                }
                else if (GameplayMessageIds.IsGameplayMessage(messageId))
                {
                    Debug.LogError("MessageId is reserved for gameplay primitives (200-209). Use 100-199 or 210+ for your game.");
                }
                else if (session != null)
                {
                    var payload = Encoding.UTF8.GetBytes(messageText ?? string.Empty);
                    session.Send(new NetworkMessage(messageId, payload), SendOptions.ToAll());
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("Logs");
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(220));
            if (gameModule != null)
            {
                var logs = gameModule.Logs;
                for (var i = 0; i < logs.Count; i++)
                {
                    GUILayout.Label(logs[i]);
                }
            }
            else
            {
                GUILayout.Label("No game module bound.");
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private NetworkSpawner GetOrCreateSpawner(MultiplayerApp app)
        {
            if (app == null)
            {
                return null;
            }

            if (cachedSpawner != null)
            {
                return cachedSpawner;
            }

            cachedSpawner = app.GetComponent<NetworkSpawner>();
            if (cachedSpawner == null)
            {
                cachedSpawner = app.gameObject.AddComponent<NetworkSpawner>();
            }

            if (cachedSpawner.PrefabCatalog == null)
            {
                cachedSpawner.SetPrefabCatalog(GetOrCreateRuntimeCatalog());
            }

            return cachedSpawner;
        }

        private NetworkPrefabCatalog GetOrCreateRuntimeCatalog()
        {
            if (runtimeCatalog != null)
            {
                return runtimeCatalog;
            }

            playerTemplate = CreatePlayerTemplate();
            ballTemplate = CreateBallTemplate();

            runtimeCatalog = ScriptableObject.CreateInstance<NetworkPrefabCatalog>();
            runtimeCatalog.SetEntries(new[]
            {
                new NetworkPrefabCatalog.Entry { PrefabId = PlayerPrefabId, Prefab = playerTemplate },
                new NetworkPrefabCatalog.Entry { PrefabId = BallPrefabId, Prefab = ballTemplate },
            });

            return runtimeCatalog;
        }

        private static GameObject CreatePlayerTemplate()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
            template.name = "PlayerTemplate";
            template.transform.localScale = new Vector3(1f, 1f, 1f);
            template.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            template.SetActive(false);

            var identity = template.AddComponent<NetworkIdentity>();
            identity.SetKind(NetworkIdentity.IdentityKind.Spawned);

            template.AddComponent<NetworkTransformSync>();

            var mover = template.AddComponent<MinimalDemoMover>();
            mover.SetKeys(KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S);
            mover.SetMoveSpeed(4f);
            mover.SetColors(local: new Color(0.2f, 0.9f, 0.2f, 1f), remote: new Color(0.9f, 0.2f, 0.2f, 1f));

            DontDestroyOnLoad(template);
            return template;
        }

        private static GameObject CreateBallTemplate()
        {
            var template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            template.name = "BallTemplate";
            template.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            template.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            template.SetActive(false);

            var identity = template.AddComponent<NetworkIdentity>();
            identity.SetKind(NetworkIdentity.IdentityKind.Spawned);

            template.AddComponent<NetworkTransformSync>();

            var mover = template.AddComponent<MinimalDemoMover>();
            mover.SetKeys(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow);
            mover.SetMoveSpeed(3.5f);
            mover.SetColors(local: new Color(0.95f, 0.85f, 0.2f, 1f), remote: new Color(0.55f, 0.55f, 0.55f, 1f));

            DontDestroyOnLoad(template);
            return template;
        }

        private static void SpawnPlayersAndBall(MultiplayerApp app, NetworkSpawner spawner)
        {
            if (app == null || spawner == null)
            {
                return;
            }

            var transport = app.Transport;
            if (transport == null || transport.RoomState != TransportRoomState.InRoom)
            {
                Debug.LogError("Join a room before spawning gameplay objects.");
                return;
            }

            if (app.Session == null || !app.Session.IsHost)
            {
                Debug.LogError("Only the host can spawn gameplay objects.");
                return;
            }

            if (spawner.SpawnCount > 0)
            {
                Debug.LogWarning("Gameplay objects already spawned. Click 'Despawn All' first.");
                return;
            }

            var players = transport.Players;
            if (players == null || players.Count == 0)
            {
                Debug.LogError("No players in room.");
                return;
            }

            var centerOffset = (players.Count - 1) * 0.5f;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }

                var position = new Vector3((i - centerOffset) * 2f, 0.5f, -2f);
                if (spawner.TrySpawnForAll(
                        PlayerPrefabId,
                        position,
                        Quaternion.identity,
                        NetworkTransformSync.AuthorityMode.Owner,
                        ownerPlayerId: player.PlayerId,
                        customPayload: Array.Empty<byte>(),
                        out var identity)
                    && identity != null)
                {
                    identity.name = $"Player_{player.PlayerName}";
                }
            }

            spawner.TrySpawnForAll(
                BallPrefabId,
                new Vector3(0f, 0.5f, 1.5f),
                Quaternion.identity,
                NetworkTransformSync.AuthorityMode.Host,
                ownerPlayerId: string.Empty,
                customPayload: Array.Empty<byte>(),
                out var ballIdentity);

            if (ballIdentity != null)
            {
                ballIdentity.name = "Ball";
            }
        }

        private static string LabeledTextField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(140));
            value = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
            return value;
        }

        private static int LabeledIntField(string label, int value)
        {
            var str = LabeledTextField(label, value.ToString());
            return int.TryParse(str, out var parsed) ? parsed : value;
        }
    }
}
