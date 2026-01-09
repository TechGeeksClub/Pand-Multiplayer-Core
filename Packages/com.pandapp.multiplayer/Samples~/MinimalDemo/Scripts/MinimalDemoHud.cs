using System.Text;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;
#if PHOTON_UNITY_NETWORKING
using Photon.Pun;
#endif

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    public sealed class MinimalDemoHud : MonoBehaviour
    {
        [Header("Defaults")]
        [SerializeField] private string playerName = "Player";
        [SerializeField] private string userId = string.Empty;
        [SerializeField] private string roomCode = "ROOM";
        [SerializeField] private byte maxPlayers = 4;
        [SerializeField] private string gameSceneName = "PandappMinimalDemo_Game";

        [Header("Message")]
        [SerializeField] private int messageId = 100;
        [SerializeField] private string messageText = "hello";

        [Header("Bindings")]
        [SerializeField] private MinimalDemoGameModule gameModule;

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

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Connect", GUILayout.Height(28)))
            {
                app.Connect(new ConnectOptions { PlayerName = playerName, UserId = userId });
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
