using Pandapp.Multiplayer.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pandapp.Multiplayer.App
{
    public sealed class UnitySceneLoader : ISceneLoader
    {
        private readonly SceneCatalog catalog;

        public UnitySceneLoader(SceneCatalog catalog)
        {
            this.catalog = catalog;
        }

        public void Load(SceneId sceneId)
        {
            if (catalog != null)
            {
                if (!catalog.TryGetSceneName(sceneId, out var sceneName))
                {
                    Debug.LogError($"[{nameof(UnitySceneLoader)}] No scene mapping for id '{sceneId}'.");
                    return;
                }

                SceneManager.LoadScene(sceneName);
                return;
            }

            if (string.IsNullOrEmpty(sceneId.Value))
            {
                Debug.LogError($"[{nameof(UnitySceneLoader)}] Invalid scene id.");
                return;
            }

            SceneManager.LoadScene(sceneId.Value);
        }
    }
}
