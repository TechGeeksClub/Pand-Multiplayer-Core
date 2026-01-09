namespace Pandapp.Multiplayer.Gameplay
{
    public static class GameplayMessageIds
    {
        public const int ReservedMin = 200;
        public const int ReservedMax = 209;

        public const int TransformState = 200;
        public const int Spawn = 201;
        public const int Despawn = 202;
        public const int OwnershipChanged = 203;

        public static bool IsGameplayMessage(int messageId)
        {
            return messageId >= ReservedMin && messageId <= ReservedMax;
        }
    }
}
