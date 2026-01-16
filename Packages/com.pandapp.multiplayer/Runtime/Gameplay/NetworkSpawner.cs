using System;
using System.Collections.Generic;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NetworkSpawner : MonoBehaviour, IGameplayMessageHandler
    {
        private struct SpawnedObject
        {
            public int PrefabId;
            public string OwnerPlayerId;
            public NetworkTransformSync.AuthorityMode Authority;
            public NetworkIdentity Identity;
            public byte[] CustomPayload;
        }

        [Header("Config")]
        [SerializeField] private MultiplayerApp app;
        [SerializeField] private NetworkPrefabCatalog prefabCatalog;

        [Header("Behaviour")]
        [SerializeField] private bool syncExistingSpawnsToNewPlayers = true;
        [SerializeField] private bool despawnOwnedObjectsOnPlayerLeft = true;

        private readonly Dictionary<int, SpawnedObject> spawnedByNetworkId = new Dictionary<int, SpawnedObject>();

        private MultiplayerGameplayRouter router;
        private int nextNetworkId = 1;

        public NetworkPrefabCatalog PrefabCatalog => prefabCatalog;
        public int SpawnCount => spawnedByNetworkId.Count;

        public void SetPrefabCatalog(NetworkPrefabCatalog catalog)
        {
            prefabCatalog = catalog;
        }

        public bool TryGetFirstSpawnedByPrefabId(int prefabId, out NetworkIdentity identity)
        {
            identity = null;

            if (prefabId <= 0)
            {
                return false;
            }

            foreach (var kvp in spawnedByNetworkId)
            {
                var spawned = kvp.Value;
                if (spawned.PrefabId == prefabId && spawned.Identity != null)
                {
                    identity = spawned.Identity;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFirstSpawnedByOwner(string ownerPlayerId, int prefabId, out NetworkIdentity identity)
        {
            identity = null;

            if (prefabId <= 0 || string.IsNullOrEmpty(ownerPlayerId))
            {
                return false;
            }

            foreach (var kvp in spawnedByNetworkId)
            {
                var spawned = kvp.Value;
                if (spawned.PrefabId == prefabId
                    && spawned.Identity != null
                    && string.Equals(spawned.OwnerPlayerId, ownerPlayerId, StringComparison.Ordinal))
                {
                    identity = spawned.Identity;
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            if (app == null)
            {
                app = GetComponent<MultiplayerApp>() ?? MultiplayerApp.Instance;
            }
        }

        private void OnEnable()
        {
            if (app == null)
            {
                app = MultiplayerApp.Instance;
            }

            if (app == null)
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] MultiplayerApp not found.", this);
                enabled = false;
                return;
            }

            MultiplayerGameplayRouter.EnsureForApp(app);
            router = app.GetComponent<MultiplayerGameplayRouter>();
            router?.RegisterHandler(this);

            if (app.Transport != null)
            {
                app.Transport.PlayerJoined += HandlePlayerJoined;
                app.Transport.PlayerLeft += HandlePlayerLeft;
                app.Transport.RoomLeft += HandleRoomLeft;
            }
        }

        private void OnDisable()
        {
            if (router != null)
            {
                router.UnregisterHandler(this);
                router = null;
            }

            if (app != null && app.Transport != null)
            {
                app.Transport.PlayerJoined -= HandlePlayerJoined;
                app.Transport.PlayerLeft -= HandlePlayerLeft;
                app.Transport.RoomLeft -= HandleRoomLeft;
            }
        }

        public bool TrySpawnForAll(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            NetworkTransformSync.AuthorityMode authority,
            string ownerPlayerId,
            byte[] customPayload,
            out NetworkIdentity identity)
        {
            identity = null;

            if (!IsInRoom(out var transport))
            {
                return false;
            }

            if (!IsLocalHost())
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] Only host can spawn objects.", this);
                return false;
            }

            if (!TrySpawnLocal(prefabId, position, rotation, authority, ownerPlayerId, customPayload, out identity, out var networkId))
            {
                return false;
            }

            var payload = SpawnMessageCodec.WriteSpawnPayload(
                new SpawnMessageCodec.SpawnData(networkId, prefabId, authority, ownerPlayerId, position, rotation, customPayload));

            transport.Send(new NetworkMessage(GameplayMessageIds.Spawn, payload), SendOptions.ToOthers(reliable: true));
            return true;
        }

        public bool TryDespawnForAll(int networkId)
        {
            if (networkId <= 0)
            {
                return false;
            }

            if (!IsInRoom(out var transport))
            {
                return false;
            }

            if (!IsLocalHost())
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] Only host can despawn objects.", this);
                return false;
            }

            if (!DespawnLocal(networkId))
            {
                return false;
            }

            var payload = SpawnMessageCodec.WriteDespawnPayload(networkId);
            transport.Send(new NetworkMessage(GameplayMessageIds.Despawn, payload), SendOptions.ToOthers(reliable: true));
            return true;
        }

        public bool TrySetOwnershipForAll(int networkId, NetworkTransformSync.AuthorityMode authority, string ownerPlayerId)
        {
            if (networkId <= 0)
            {
                return false;
            }

            if (!IsInRoom(out var transport))
            {
                return false;
            }

            if (!IsLocalHost())
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] Only host can change ownership.", this);
                return false;
            }

            if (!TryApplyOwnershipLocal(networkId, authority, ownerPlayerId))
            {
                return false;
            }

            var payload = SpawnMessageCodec.WriteOwnershipPayload(
                new SpawnMessageCodec.OwnershipData(networkId, authority, ownerPlayerId));
            transport.Send(new NetworkMessage(GameplayMessageIds.OwnershipChanged, payload), SendOptions.ToOthers(reliable: true));
            return true;
        }

        bool IGameplayMessageHandler.HandleMessage(NetworkMessage message)
        {
            if (message == null)
            {
                return false;
            }

            switch (message.MessageId)
            {
                case GameplayMessageIds.Spawn:
                    HandleRemoteSpawn(message);
                    return true;

                case GameplayMessageIds.Despawn:
                    HandleRemoteDespawn(message);
                    return true;

                case GameplayMessageIds.OwnershipChanged:
                    HandleRemoteOwnershipChanged(message);
                    return true;

                default:
                    return false;
            }
        }

        private void HandleRemoteSpawn(NetworkMessage message)
        {
            if (!IsInRoom(out _))
            {
                return;
            }

            if (!IsMessageFromHost(message))
            {
                return;
            }

            if (!SpawnMessageCodec.TryReadSpawnPayload(message.Payload, out var data))
            {
                return;
            }

            if (data.NetworkId <= 0 || data.PrefabId <= 0)
            {
                return;
            }

            if (spawnedByNetworkId.ContainsKey(data.NetworkId))
            {
                return;
            }

            TrySpawnLocal(
                data.PrefabId,
                data.Position,
                data.Rotation,
                data.Authority,
                data.OwnerPlayerId,
                data.CustomPayload,
                out _,
                data.NetworkId);
        }

        private void HandleRemoteDespawn(NetworkMessage message)
        {
            if (!IsInRoom(out _))
            {
                return;
            }

            if (!IsMessageFromHost(message))
            {
                return;
            }

            if (!SpawnMessageCodec.TryReadDespawnPayload(message.Payload, out var networkId))
            {
                return;
            }

            DespawnLocal(networkId);
        }

        private void HandleRemoteOwnershipChanged(NetworkMessage message)
        {
            if (!IsInRoom(out _))
            {
                return;
            }

            if (!IsMessageFromHost(message))
            {
                return;
            }

            if (!SpawnMessageCodec.TryReadOwnershipPayload(message.Payload, out var data))
            {
                return;
            }

            TryApplyOwnershipLocal(data.NetworkId, data.Authority, data.OwnerPlayerId);
        }

        private bool TrySpawnLocal(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            NetworkTransformSync.AuthorityMode authority,
            string ownerPlayerId,
            byte[] customPayload,
            out NetworkIdentity identity,
            out int networkId)
        {
            identity = null;
            networkId = 0;

            if (prefabId <= 0)
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] PrefabId must be > 0.", this);
                return false;
            }

            if (prefabCatalog == null)
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] PrefabCatalog is not assigned.", this);
                return false;
            }

            if (!prefabCatalog.TryGetPrefab(prefabId, out var prefab) || prefab == null)
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] PrefabId {prefabId} not found in catalog.", this);
                return false;
            }

            networkId = AllocateNetworkId();
            if (!TrySpawnLocal(prefabId, position, rotation, authority, ownerPlayerId, customPayload, out identity, networkId))
            {
                networkId = 0;
                return false;
            }

            return true;
        }

        private bool TrySpawnLocal(
            int prefabId,
            Vector3 position,
            Quaternion rotation,
            NetworkTransformSync.AuthorityMode authority,
            string ownerPlayerId,
            byte[] customPayload,
            out NetworkIdentity identity,
            int networkId)
        {
            identity = null;

            if (networkId <= 0)
            {
                return false;
            }

            if (prefabCatalog == null)
            {
                return false;
            }

            if (!prefabCatalog.TryGetPrefab(prefabId, out var prefab) || prefab == null)
            {
                return false;
            }

            var instance = Instantiate(prefab, position, rotation);
            if (instance == null)
            {
                return false;
            }

            if (!instance.TryGetComponent<NetworkIdentity>(out identity) || identity == null)
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] Spawned prefab '{prefab.name}' is missing {nameof(NetworkIdentity)}.", instance);
                Destroy(instance);
                identity = null;
                return false;
            }

            identity.SetKind(NetworkIdentity.IdentityKind.Spawned);

            if (!identity.TrySetNetworkId(networkId))
            {
                Destroy(instance);
                identity = null;
                return false;
            }

            if (instance.TryGetComponent<NetworkTransformSync>(out var transformSync) && transformSync != null)
            {
                transformSync.SetAuthority(authority);
                transformSync.SetOwner(ownerPlayerId);
            }

            MoveToActiveScene(instance);
            if (!instance.activeSelf)
            {
                instance.SetActive(true);
            }

            spawnedByNetworkId[networkId] = new SpawnedObject
            {
                PrefabId = prefabId,
                OwnerPlayerId = ownerPlayerId ?? string.Empty,
                Authority = authority,
                Identity = identity,
                CustomPayload = customPayload ?? Array.Empty<byte>(),
            };

            return true;
        }

        private bool DespawnLocal(int networkId)
        {
            if (!spawnedByNetworkId.TryGetValue(networkId, out var spawned))
            {
                return false;
            }

            spawnedByNetworkId.Remove(networkId);

            if (spawned.Identity != null)
            {
                Destroy(spawned.Identity.gameObject);
            }

            return true;
        }

        public bool TryDespawnAllForAll()
        {
            if (!IsInRoom(out _))
            {
                return false;
            }

            if (!IsLocalHost())
            {
                Debug.LogError($"[{nameof(NetworkSpawner)}] Only host can despawn objects.", this);
                return false;
            }

            if (spawnedByNetworkId.Count == 0)
            {
                return true;
            }

            var ids = ListPool<int>.Get();
            try
            {
                foreach (var kvp in spawnedByNetworkId)
                {
                    ids.Add(kvp.Key);
                }

                for (var i = 0; i < ids.Count; i++)
                {
                    TryDespawnForAll(ids[i]);
                }
            }
            finally
            {
                ListPool<int>.Release(ids);
            }

            return true;
        }

        private bool TryApplyOwnershipLocal(int networkId, NetworkTransformSync.AuthorityMode authority, string ownerPlayerId)
        {
            if (!spawnedByNetworkId.TryGetValue(networkId, out var spawned) || spawned.Identity == null)
            {
                return false;
            }

            var instance = spawned.Identity.gameObject;
            if (instance != null && instance.TryGetComponent<NetworkTransformSync>(out var transformSync) && transformSync != null)
            {
                transformSync.SetAuthority(authority);
                transformSync.SetOwner(ownerPlayerId);
            }

            spawned.Authority = authority;
            spawned.OwnerPlayerId = ownerPlayerId ?? string.Empty;
            spawnedByNetworkId[networkId] = spawned;

            return true;
        }

        private void HandlePlayerJoined(PlayerInfo player)
        {
            if (!syncExistingSpawnsToNewPlayers)
            {
                return;
            }

            if (player == null || string.IsNullOrEmpty(player.PlayerId))
            {
                return;
            }

            if (!IsInRoom(out var transport))
            {
                return;
            }

            if (!IsLocalHost())
            {
                return;
            }

            foreach (var kvp in spawnedByNetworkId)
            {
                var spawned = kvp.Value;
                if (spawned.Identity == null)
                {
                    continue;
                }

                var payload = SpawnMessageCodec.WriteSpawnPayload(
                    new SpawnMessageCodec.SpawnData(
                        kvp.Key,
                        spawned.PrefabId,
                        spawned.Authority,
                        spawned.OwnerPlayerId,
                        spawned.Identity.transform.position,
                        spawned.Identity.transform.rotation,
                        spawned.CustomPayload));

                transport.Send(new NetworkMessage(GameplayMessageIds.Spawn, payload), SendOptions.ToPlayer(player.PlayerId, reliable: true));
            }
        }

        private void HandlePlayerLeft(PlayerInfo player)
        {
            if (!despawnOwnedObjectsOnPlayerLeft)
            {
                return;
            }

            if (player == null || string.IsNullOrEmpty(player.PlayerId))
            {
                return;
            }

            if (!IsInRoom(out _))
            {
                return;
            }

            if (!IsLocalHost())
            {
                return;
            }

            var toDespawn = ListPool<int>.Get();
            try
            {
                foreach (var kvp in spawnedByNetworkId)
                {
                    if (string.Equals(kvp.Value.OwnerPlayerId, player.PlayerId, StringComparison.Ordinal))
                    {
                        toDespawn.Add(kvp.Key);
                    }
                }

                for (var i = 0; i < toDespawn.Count; i++)
                {
                    TryDespawnForAll(toDespawn[i]);
                }
            }
            finally
            {
                ListPool<int>.Release(toDespawn);
            }
        }

        private void HandleRoomLeft()
        {
            ClearLocal();
        }

        private void ClearLocal()
        {
            foreach (var kvp in spawnedByNetworkId)
            {
                if (kvp.Value.Identity != null)
                {
                    Destroy(kvp.Value.Identity.gameObject);
                }
            }

            spawnedByNetworkId.Clear();
        }

        private bool IsInRoom(out INetworkTransport transport)
        {
            transport = app != null ? app.Transport : null;
            return transport != null && transport.RoomState == TransportRoomState.InRoom;
        }

        private bool IsLocalHost()
        {
            return app != null && app.Session != null && app.Session.IsHost;
        }

        private bool IsMessageFromHost(NetworkMessage message)
        {
            if (app == null || app.Transport == null || message == null)
            {
                return false;
            }

            var senderId = message.SenderId;
            if (string.IsNullOrEmpty(senderId))
            {
                return false;
            }

            var players = app.Transport.Players;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.IsHost)
                {
                    return string.Equals(player.PlayerId, senderId, StringComparison.Ordinal);
                }
            }

            return false;
        }

        private int AllocateNetworkId()
        {
            if (nextNetworkId <= 0)
            {
                nextNetworkId = 1;
            }

            for (var attempts = 0; attempts < 1024; attempts++)
            {
                var id = nextNetworkId++;
                if (nextNetworkId <= 0)
                {
                    nextNetworkId = 1;
                }

                if (id <= 0)
                {
                    continue;
                }

                if (!NetworkObjectRegistry.TryGet(id, out _))
                {
                    return id;
                }
            }

            var fallback = Guid.NewGuid().GetHashCode() & 0x7fffffff;
            return fallback == 0 ? 1 : fallback;
        }

        private static void MoveToActiveScene(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                return;
            }

            if (instance.scene == activeScene)
            {
                return;
            }

            SceneManager.MoveGameObjectToScene(instance, activeScene);
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                if (list == null)
                {
                    return;
                }

                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
