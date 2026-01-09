namespace Pandapp.Multiplayer.Core
{
    public enum SessionState
    {
        Offline = 0,
        Connecting = 1,
        Online = 2,
        CreatingRoom = 3,
        JoiningRoom = 4,
        InRoom = 5,
        StartingGame = 6,
        InGame = 7,
    }
}
