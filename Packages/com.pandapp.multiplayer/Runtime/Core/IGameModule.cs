namespace Pandapp.Multiplayer.Core
{
    public interface IGameModule
    {
        void OnPlayerJoined(PlayerInfo player);
        void OnPlayerLeft(PlayerInfo player);
        void OnMessageReceived(NetworkMessage msg);
        void OnUpdate(float deltaTime);
    }
}
