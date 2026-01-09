using Pandapp.Multiplayer.Core;

namespace Pandapp.Multiplayer.Gameplay
{
    public interface IGameplayMessageHandler
    {
        bool HandleMessage(NetworkMessage message);
    }
}

