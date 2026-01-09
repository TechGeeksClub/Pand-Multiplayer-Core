using System;
using System.Text;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    internal static class SpawnMessageCodec
    {
        internal readonly struct SpawnData
        {
            public readonly int NetworkId;
            public readonly int PrefabId;
            public readonly NetworkTransformSync.AuthorityMode Authority;
            public readonly string OwnerPlayerId;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly byte[] CustomPayload;

            public SpawnData(
                int networkId,
                int prefabId,
                NetworkTransformSync.AuthorityMode authority,
                string ownerPlayerId,
                Vector3 position,
                Quaternion rotation,
                byte[] customPayload)
            {
                NetworkId = networkId;
                PrefabId = prefabId;
                Authority = authority;
                OwnerPlayerId = ownerPlayerId ?? string.Empty;
                Position = position;
                Rotation = rotation;
                CustomPayload = customPayload ?? Array.Empty<byte>();
            }
        }

        internal readonly struct OwnershipData
        {
            public readonly int NetworkId;
            public readonly NetworkTransformSync.AuthorityMode Authority;
            public readonly string OwnerPlayerId;

            public OwnershipData(int networkId, NetworkTransformSync.AuthorityMode authority, string ownerPlayerId)
            {
                NetworkId = networkId;
                Authority = authority;
                OwnerPlayerId = ownerPlayerId ?? string.Empty;
            }
        }

        public static byte[] WriteSpawnPayload(SpawnData data)
        {
            var ownerId = data.OwnerPlayerId ?? string.Empty;
            var ownerByteCount = Encoding.UTF8.GetByteCount(ownerId);
            if (ownerByteCount > ushort.MaxValue)
            {
                throw new ArgumentException("OwnerPlayerId is too long.", nameof(data));
            }

            var customPayload = data.CustomPayload ?? Array.Empty<byte>();

            var size = 4 + 4 + 1 + 2 + ownerByteCount + 12 + 16 + 4 + customPayload.Length;
            var payload = new byte[size];

            var offset = 0;
            WriteInt32(payload, ref offset, data.NetworkId);
            WriteInt32(payload, ref offset, data.PrefabId);
            payload[offset++] = (byte)data.Authority;
            WriteUInt16(payload, ref offset, (ushort)ownerByteCount);
            if (ownerByteCount > 0)
            {
                Encoding.UTF8.GetBytes(ownerId, 0, ownerId.Length, payload, offset);
                offset += ownerByteCount;
            }

            WriteVector3(payload, ref offset, data.Position);
            WriteQuaternion(payload, ref offset, data.Rotation);

            WriteInt32(payload, ref offset, customPayload.Length);
            if (customPayload.Length > 0)
            {
                Buffer.BlockCopy(customPayload, 0, payload, offset, customPayload.Length);
                offset += customPayload.Length;
            }

            return payload;
        }

        public static bool TryReadSpawnPayload(byte[] payload, out SpawnData data)
        {
            data = default;

            if (payload == null || payload.Length < 43)
            {
                return false;
            }

            var offset = 0;
            if (!TryReadInt32(payload, ref offset, out var networkId))
            {
                return false;
            }

            if (!TryReadInt32(payload, ref offset, out var prefabId))
            {
                return false;
            }

            if (offset >= payload.Length)
            {
                return false;
            }

            var authority = (NetworkTransformSync.AuthorityMode)payload[offset++];

            if (!TryReadUInt16(payload, ref offset, out var ownerByteCount))
            {
                return false;
            }

            if (payload.Length < offset + ownerByteCount + 12 + 16 + 4)
            {
                return false;
            }

            var ownerId = ownerByteCount == 0
                ? string.Empty
                : Encoding.UTF8.GetString(payload, offset, ownerByteCount);
            offset += ownerByteCount;

            if (!TryReadVector3(payload, ref offset, out var position))
            {
                return false;
            }

            if (!TryReadQuaternion(payload, ref offset, out var rotation))
            {
                return false;
            }

            if (!TryReadInt32(payload, ref offset, out var customPayloadLength))
            {
                return false;
            }

            if (customPayloadLength < 0 || payload.Length < offset + customPayloadLength)
            {
                return false;
            }

            byte[] customPayload;
            if (customPayloadLength == 0)
            {
                customPayload = Array.Empty<byte>();
            }
            else
            {
                customPayload = new byte[customPayloadLength];
                Buffer.BlockCopy(payload, offset, customPayload, 0, customPayloadLength);
                offset += customPayloadLength;
            }

            data = new SpawnData(networkId, prefabId, authority, ownerId, position, rotation, customPayload);
            return true;
        }

        public static byte[] WriteDespawnPayload(int networkId)
        {
            var payload = new byte[4];
            var offset = 0;
            WriteInt32(payload, ref offset, networkId);
            return payload;
        }

        public static bool TryReadDespawnPayload(byte[] payload, out int networkId)
        {
            networkId = 0;
            if (payload == null || payload.Length != 4)
            {
                return false;
            }

            var offset = 0;
            return TryReadInt32(payload, ref offset, out networkId);
        }

        public static byte[] WriteOwnershipPayload(OwnershipData data)
        {
            var ownerId = data.OwnerPlayerId ?? string.Empty;
            var ownerByteCount = Encoding.UTF8.GetByteCount(ownerId);
            if (ownerByteCount > ushort.MaxValue)
            {
                throw new ArgumentException("OwnerPlayerId is too long.", nameof(data));
            }

            var size = 4 + 1 + 2 + ownerByteCount;
            var payload = new byte[size];

            var offset = 0;
            WriteInt32(payload, ref offset, data.NetworkId);
            payload[offset++] = (byte)data.Authority;
            WriteUInt16(payload, ref offset, (ushort)ownerByteCount);
            if (ownerByteCount > 0)
            {
                Encoding.UTF8.GetBytes(ownerId, 0, ownerId.Length, payload, offset);
                offset += ownerByteCount;
            }

            return payload;
        }

        public static bool TryReadOwnershipPayload(byte[] payload, out OwnershipData data)
        {
            data = default;
            if (payload == null || payload.Length < 7)
            {
                return false;
            }

            var offset = 0;
            if (!TryReadInt32(payload, ref offset, out var networkId))
            {
                return false;
            }

            if (offset >= payload.Length)
            {
                return false;
            }

            var authority = (NetworkTransformSync.AuthorityMode)payload[offset++];

            if (!TryReadUInt16(payload, ref offset, out var ownerByteCount))
            {
                return false;
            }

            if (payload.Length < offset + ownerByteCount)
            {
                return false;
            }

            var ownerId = ownerByteCount == 0
                ? string.Empty
                : Encoding.UTF8.GetString(payload, offset, ownerByteCount);

            data = new OwnershipData(networkId, authority, ownerId);
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

        private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }

        private static bool TryReadUInt16(byte[] buffer, ref int offset, out int value)
        {
            value = 0;
            if (buffer == null || buffer.Length < offset + 2)
            {
                return false;
            }

            value = buffer[offset] | (buffer[offset + 1] << 8);
            offset += 2;
            return true;
        }

        private static void WriteVector3(byte[] buffer, ref int offset, Vector3 value)
        {
            WriteSingle(buffer, ref offset, value.x);
            WriteSingle(buffer, ref offset, value.y);
            WriteSingle(buffer, ref offset, value.z);
        }

        private static bool TryReadVector3(byte[] buffer, ref int offset, out Vector3 value)
        {
            value = default;
            if (!TryReadSingle(buffer, ref offset, out var x))
            {
                return false;
            }
            if (!TryReadSingle(buffer, ref offset, out var y))
            {
                return false;
            }
            if (!TryReadSingle(buffer, ref offset, out var z))
            {
                return false;
            }

            value = new Vector3(x, y, z);
            return true;
        }

        private static void WriteQuaternion(byte[] buffer, ref int offset, Quaternion value)
        {
            WriteSingle(buffer, ref offset, value.x);
            WriteSingle(buffer, ref offset, value.y);
            WriteSingle(buffer, ref offset, value.z);
            WriteSingle(buffer, ref offset, value.w);
        }

        private static bool TryReadQuaternion(byte[] buffer, ref int offset, out Quaternion value)
        {
            value = default;
            if (!TryReadSingle(buffer, ref offset, out var x))
            {
                return false;
            }
            if (!TryReadSingle(buffer, ref offset, out var y))
            {
                return false;
            }
            if (!TryReadSingle(buffer, ref offset, out var z))
            {
                return false;
            }
            if (!TryReadSingle(buffer, ref offset, out var w))
            {
                return false;
            }

            value = new Quaternion(x, y, z, w);
            return true;
        }

        private static void WriteSingle(byte[] buffer, ref int offset, float value)
        {
            WriteInt32(buffer, ref offset, BitConverter.SingleToInt32Bits(value));
        }

        private static bool TryReadSingle(byte[] buffer, ref int offset, out float value)
        {
            value = 0f;
            if (!TryReadInt32(buffer, ref offset, out var bits))
            {
                return false;
            }

            value = BitConverter.Int32BitsToSingle(bits);
            return true;
        }
    }
}

