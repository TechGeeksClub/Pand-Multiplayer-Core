using System.Collections.Generic;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    public static class NetworkObjectRegistry
    {
        private static readonly Dictionary<int, NetworkIdentity> IdToIdentity = new Dictionary<int, NetworkIdentity>();

        public static bool TryGet(int networkId, out NetworkIdentity identity)
        {
            return IdToIdentity.TryGetValue(networkId, out identity);
        }

        internal static void Register(NetworkIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            var id = identity.NetworkId;
            if (id <= 0)
            {
                return;
            }

            if (IdToIdentity.TryGetValue(id, out var existing) && existing != null && existing != identity)
            {
                Debug.LogError(
                    $"[{nameof(NetworkObjectRegistry)}] Duplicate NetworkId detected: {id} ('{existing.name}' and '{identity.name}').",
                    identity);
                return;
            }

            IdToIdentity[id] = identity;
        }

        internal static void Unregister(NetworkIdentity identity)
        {
            if (identity == null)
            {
                return;
            }

            var id = identity.NetworkId;
            if (!IdToIdentity.TryGetValue(id, out var existing) || existing != identity)
            {
                return;
            }

            IdToIdentity.Remove(id);
        }
    }
}

