namespace PandNet.Core
{
    public interface IGameModule
    {
        void OnPlayerJoined(PlayerInfo player);
        void OnPlayerLeft(PlayerInfo player);
        void OnMessageReceived(GameMessage msg);
        void OnUpdate(float deltaTime);
    }
}
