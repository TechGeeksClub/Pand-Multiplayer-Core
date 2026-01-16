using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.App
{
    public abstract class GameModuleBehaviourBase : MonoBehaviour, IGameModule
    {
        public virtual void OnPlayerJoined(PlayerInfo player) {}
        public virtual void OnPlayerLeft(PlayerInfo player) {}
        public virtual void OnMessageReceived(NetworkMessage msg) {}
        public virtual void OnUpdate(float deltaTime) {}
    }
}

