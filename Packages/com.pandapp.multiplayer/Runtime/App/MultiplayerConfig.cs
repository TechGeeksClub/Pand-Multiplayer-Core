using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.App
{
    [CreateAssetMenu(fileName = "MultiplayerConfig", menuName = "Pandapp/Multiplayer/Multiplayer Config")]
    public sealed class MultiplayerConfig : ScriptableObject
    {
        [Header("Scenes")]
        [SerializeField] private SceneCatalog sceneCatalog;
        [SerializeField] private SceneId gameScene;

        [Header("Defaults")]
        [SerializeField] private byte defaultMaxPlayers = 4;

        public SceneCatalog SceneCatalog => sceneCatalog;
        public SceneId GameScene => gameScene;
        public byte DefaultMaxPlayers => defaultMaxPlayers;

        public RoomOptions CreateRoomOptions(string roomCode)
        {
            return new RoomOptions
            {
                RoomCode = roomCode,
                MaxPlayers = defaultMaxPlayers,
                IsOpen = true,
                IsVisible = true,
            };
        }
    }
}

