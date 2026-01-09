using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [CreateAssetMenu(
        fileName = "NetworkPrefabCatalog",
        menuName = "Pandapp/Multiplayer/Gameplay/Network Prefab Catalog")]
    public sealed class NetworkPrefabCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int PrefabId;
            public GameObject Prefab;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<int, GameObject> prefabById;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetPrefab(int prefabId, out GameObject prefab)
        {
            EnsureCache();
            return prefabById.TryGetValue(prefabId, out prefab) && prefab != null;
        }

        public void SetEntries(IReadOnlyList<Entry> newEntries)
        {
            if (newEntries == null || newEntries.Count == 0)
            {
                entries = Array.Empty<Entry>();
                prefabById = null;
                return;
            }

            entries = new Entry[newEntries.Count];
            for (var i = 0; i < newEntries.Count; i++)
            {
                entries[i] = newEntries[i];
            }

            prefabById = null;
        }

        private void OnEnable()
        {
            prefabById = null;
        }

        private void OnValidate()
        {
            prefabById = null;
        }

        private void EnsureCache()
        {
            if (prefabById != null)
            {
                return;
            }

            prefabById = new Dictionary<int, GameObject>();

            if (entries == null || entries.Length == 0)
            {
                return;
            }

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.PrefabId <= 0 || entry.Prefab == null)
                {
                    continue;
                }

                if (prefabById.TryGetValue(entry.PrefabId, out var existing) && existing != null && existing != entry.Prefab)
                {
                    Debug.LogError(
                        $"[{nameof(NetworkPrefabCatalog)}] Duplicate PrefabId {entry.PrefabId} ('{existing.name}' and '{entry.Prefab.name}').",
                        this);
                    continue;
                }

                prefabById[entry.PrefabId] = entry.Prefab;
            }
        }
    }
}

