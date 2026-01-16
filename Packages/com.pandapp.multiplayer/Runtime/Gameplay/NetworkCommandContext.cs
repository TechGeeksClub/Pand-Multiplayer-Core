using System;

namespace Pandapp.Multiplayer.Gameplay
{
    public readonly struct NetworkCommandContext
    {
        public ushort CommandId { get; }
        public int TargetNetworkId { get; }
        public NetworkIdentity Target { get; }
        public string SenderId { get; }
        public ArraySegment<byte> Payload { get; }

        public NetworkCommandContext(
            ushort commandId,
            int targetNetworkId,
            NetworkIdentity target,
            string senderId,
            ArraySegment<byte> payload)
        {
            CommandId = commandId;
            TargetNetworkId = targetNetworkId;
            Target = target;
            SenderId = senderId ?? string.Empty;
            Payload = payload.Array == null ? new ArraySegment<byte>(Array.Empty<byte>()) : payload;
        }
    }
}

