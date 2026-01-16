namespace Pandapp.Multiplayer.Gameplay
{
    public interface INetworkCommandHandler
    {
        bool HandleCommand(NetworkCommandContext context);
    }
}

