namespace Pandapp.Multiplayer.Gameplay
{
    public static class GameplayMessageIds
    {
        public const int ReservedMin = 180;
        public const int ReservedMax = 199;

        public const int TransformState = 180;
        public const int Spawn = 181;
        public const int Despawn = 182;
        public const int OwnershipChanged = 183;
        public const int Command = 184;
        public const int Rigidbody2DState = 185;

        public static bool IsGameplayMessage(int messageId)
        {
            return messageId >= ReservedMin && messageId <= ReservedMax;
        }
    }
}
