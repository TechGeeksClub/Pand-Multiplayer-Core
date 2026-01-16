using System;

namespace Pandapp.Multiplayer.Gameplay
{
    internal static class NetworkCommandCodec
    {
        private const int HeaderSize = 6;

        public static byte[] WritePayload(ushort commandId, int targetNetworkId, byte[] payload)
        {
            payload ??= Array.Empty<byte>();

            var buffer = new byte[HeaderSize + payload.Length];
            var offset = 0;

            WriteUInt16(buffer, ref offset, commandId);
            WriteInt32(buffer, ref offset, targetNetworkId);

            if (payload.Length > 0)
            {
                Buffer.BlockCopy(payload, 0, buffer, offset, payload.Length);
            }

            return buffer;
        }

        public static bool TryReadPayload(byte[] payload, out ushort commandId, out int targetNetworkId, out ArraySegment<byte> commandPayload)
        {
            commandId = 0;
            targetNetworkId = 0;
            commandPayload = new ArraySegment<byte>(Array.Empty<byte>());

            if (payload == null || payload.Length < HeaderSize)
            {
                return false;
            }

            var offset = 0;
            if (!TryReadUInt16(payload, ref offset, out commandId))
            {
                return false;
            }

            if (!TryReadInt32(payload, ref offset, out targetNetworkId))
            {
                return false;
            }

            if (offset > payload.Length)
            {
                return false;
            }

            var count = payload.Length - offset;
            commandPayload = count == 0
                ? new ArraySegment<byte>(Array.Empty<byte>())
                : new ArraySegment<byte>(payload, offset, count);

            return true;
        }

        private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }

        private static bool TryReadUInt16(byte[] buffer, ref int offset, out ushort value)
        {
            value = 0;
            if (buffer == null || buffer.Length < offset + 2)
            {
                return false;
            }

            value = (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
            offset += 2;
            return true;
        }

        private static void WriteInt32(byte[] buffer, ref int offset, int value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 24);
        }

        private static bool TryReadInt32(byte[] buffer, ref int offset, out int value)
        {
            value = 0;
            if (buffer == null || buffer.Length < offset + 4)
            {
                return false;
            }

            value = buffer[offset]
                | (buffer[offset + 1] << 8)
                | (buffer[offset + 2] << 16)
                | (buffer[offset + 3] << 24);
            offset += 4;
            return true;
        }
    }
}

