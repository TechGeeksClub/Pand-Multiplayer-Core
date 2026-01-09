using System;

namespace Pandapp.Multiplayer.Core
{
    [Serializable]
    public struct SceneId
    {
        public string Value;

        public SceneId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public override string ToString() => Value ?? string.Empty;
    }

    public interface ISceneLoader
    {
        void Load(SceneId sceneId);
    }
}
