using System;
using System.Collections.Generic;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.App
{
    [CreateAssetMenu(fileName = "SceneCatalog", menuName = "Pandapp/Multiplayer/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SceneId Id;
            public string SceneName;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public bool TryGetSceneName(SceneId id, out string sceneName)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.Equals(entry.Id.Value, id.Value, StringComparison.Ordinal))
                {
                    sceneName = entry.SceneName;
                    return !string.IsNullOrEmpty(sceneName);
                }
            }

            sceneName = string.Empty;
            return false;
        }
    }
}
