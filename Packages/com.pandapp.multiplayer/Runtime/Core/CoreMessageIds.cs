namespace Pandapp.Multiplayer.Core
{
    public static class CoreMessageIds
    {
        public const int ReservedMin = 1;
        public const int ReservedMax = 99;

        public const int PlayerReadyChanged = 1;
        public const int GameStartRequested = 2;

        public static bool IsCoreMessage(int messageId)
        {
            return messageId >= ReservedMin && messageId <= ReservedMax;
        }
    }
}
