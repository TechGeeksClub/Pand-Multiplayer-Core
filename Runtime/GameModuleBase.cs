namespace PandNet.Core
{
    public abstract class GameModuleBase : IGameModule
    {
        public virtual void OnPlayerJoined(PlayerInfo player) {}
        public virtual void OnPlayerLeft(PlayerInfo player) {}
        public virtual void OnMessageReceived(GameMessage msg) {}
        public virtual void OnUpdate(float deltaTime) {}
    }
}
