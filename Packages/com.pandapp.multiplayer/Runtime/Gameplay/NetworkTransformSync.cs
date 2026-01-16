using System;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class NetworkTransformSync : MonoBehaviour
    {
        internal const byte PositionFlag = 1 << 0;
        internal const byte RotationFlag = 1 << 1;

        public enum AuthorityMode
        {
            Host = 0,
            Owner = 1,
        }

        [Header("Authority")]
        [SerializeField] private AuthorityMode authority = AuthorityMode.Host;
        [SerializeField] private string ownerPlayerId = string.Empty;

        [Header("Send")]
        [Min(0f)]
        [SerializeField] private float sendIntervalSeconds = 0.05f;
        [Min(0f)]
        [SerializeField] private float positionThreshold = 0.01f;
        [Min(0f)]
        [SerializeField] private float rotationThresholdDegrees = 0.5f;
        [SerializeField] private bool syncPosition = true;
        [SerializeField] private bool syncRotation = true;

        [Header("Receive")]
        [Min(0f)]
        [SerializeField] private float interpolationSpeed = 12f;

        private NetworkIdentity identity;
        private Rigidbody2D body2D;

        private float sendTimer;
        private Vector3 lastSentPosition;
        private Quaternion lastSentRotation;
        private bool hasSentOnce;

        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private bool hasTarget;

        public string OwnerPlayerId => ownerPlayerId;
        public AuthorityMode Authority => authority;

        public void SetOwner(string playerId)
        {
            ownerPlayerId = playerId ?? string.Empty;
        }

        public void SetAuthority(AuthorityMode mode)
        {
            if (authority == mode)
            {
                return;
            }

            authority = mode;
            sendTimer = 0f;
            hasSentOnce = false;
        }

        public void SetSendSettings(
            float intervalSeconds,
            float positionThresholdValue,
            float rotationThresholdDegreesValue,
            bool syncPositionValue,
            bool syncRotationValue)
        {
            sendIntervalSeconds = Mathf.Max(0f, intervalSeconds);
            positionThreshold = Mathf.Max(0f, positionThresholdValue);
            rotationThresholdDegrees = Mathf.Max(0f, rotationThresholdDegreesValue);
            syncPosition = syncPositionValue;
            syncRotation = syncRotationValue;

            sendTimer = 0f;
            hasSentOnce = false;
        }

        public void SetReceiveSettings(float interpolationSpeedValue)
        {
            interpolationSpeed = Mathf.Max(0f, interpolationSpeedValue);
        }

        private void Awake()
        {
            identity = GetComponent<NetworkIdentity>();
            body2D = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            MultiplayerGameplayRouter.EnsureForApp(MultiplayerApp.Instance);
        }

        private void Update()
        {
            if (!IsReadyForGameplay())
            {
                return;
            }

            if (IsLocalAuthority())
            {
                sendTimer += Time.deltaTime;
                if (sendTimer >= sendIntervalSeconds)
                {
                    sendTimer = 0f;
                    TrySendState();
                }
                return;
            }

            if (hasTarget)
            {
                if (!ShouldDriveRigidbody2D())
                {
                    ApplyInterpolationTransform(Time.deltaTime);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!IsReadyForGameplay())
            {
                return;
            }

            if (IsLocalAuthority())
            {
                return;
            }

            if (!hasTarget)
            {
                return;
            }

            if (ShouldDriveRigidbody2D())
            {
                ApplyInterpolationRigidbody2D(Time.fixedDeltaTime);
            }
        }

        internal void ApplyRemoteState(Vector3? position, Quaternion? rotation, string senderId)
        {
            if (!IsReadyForGameplay())
            {
                return;
            }

            if (IsLocalAuthority())
            {
                return;
            }

            if (position.HasValue)
            {
                targetPosition = position.Value;
            }

            if (rotation.HasValue)
            {
                targetRotation = rotation.Value;
            }

            hasTarget = true;
        }

        private bool IsReadyForGameplay()
        {
            var app = MultiplayerApp.Instance;
            if (app == null || app.Transport == null)
            {
                return false;
            }

            if (app.Transport.RoomState != TransportRoomState.InRoom)
            {
                return false;
            }

            return identity != null && identity.NetworkId > 0;
        }

        private bool IsLocalAuthority()
        {
            var app = MultiplayerApp.Instance;
            if (app == null || app.Transport == null)
            {
                return false;
            }

            switch (authority)
            {
                case AuthorityMode.Host:
                    return app.Session != null && app.Session.IsHost;

                case AuthorityMode.Owner:
                {
                    var localPlayerId = app.Transport.LocalPlayerId;
                    return !string.IsNullOrEmpty(localPlayerId)
                        && !string.IsNullOrEmpty(ownerPlayerId)
                        && string.Equals(localPlayerId, ownerPlayerId, StringComparison.Ordinal);
                }

                default:
                    return false;
            }
        }

        private void TrySendState()
        {
            var app = MultiplayerApp.Instance;
            var transport = app.Transport;
            if (transport == null)
            {
                return;
            }

            var position = transform.position;
            var rotation = transform.rotation;

            var flags = (byte)0;
            if (syncPosition && ShouldSendPosition(position))
            {
                flags |= PositionFlag;
            }
            if (syncRotation && ShouldSendRotation(rotation))
            {
                flags |= RotationFlag;
            }

            if (flags == 0)
            {
                return;
            }

            if ((flags & PositionFlag) != 0)
            {
                lastSentPosition = position;
            }
            if ((flags & RotationFlag) != 0)
            {
                lastSentRotation = rotation;
            }

            hasSentOnce = true;

            var payload = BuildPayload(identity.NetworkId, flags, position, rotation);
            transport.Send(
                new NetworkMessage(GameplayMessageIds.TransformState, payload),
                SendOptions.ToOthers(reliable: false));
        }

        private bool ShouldSendPosition(Vector3 position)
        {
            if (!hasSentOnce)
            {
                return true;
            }

            var thresholdSqr = positionThreshold * positionThreshold;
            return (position - lastSentPosition).sqrMagnitude >= thresholdSqr;
        }

        private bool ShouldSendRotation(Quaternion rotation)
        {
            if (!hasSentOnce)
            {
                return true;
            }

            return Quaternion.Angle(rotation, lastSentRotation) >= rotationThresholdDegrees;
        }

        private bool ShouldDriveRigidbody2D()
        {
            return body2D != null
                && body2D.simulated
                && body2D.bodyType == RigidbodyType2D.Kinematic
                && syncPosition;
        }

        private void ApplyInterpolationTransform(float deltaTime)
        {
            if (interpolationSpeed <= 0f)
            {
                if (syncPosition)
                {
                    transform.position = targetPosition;
                }
                if (syncRotation)
                {
                    transform.rotation = targetRotation;
                }
                return;
            }

            var t = 1f - Mathf.Exp(-interpolationSpeed * deltaTime);

            if (syncPosition)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            }

            if (syncRotation)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
            }
        }

        private void ApplyInterpolationRigidbody2D(float deltaTime)
        {
            if (body2D == null)
            {
                return;
            }

            if (interpolationSpeed <= 0f)
            {
                body2D.MovePosition((Vector2)targetPosition);
                return;
            }

            var t = 1f - Mathf.Exp(-interpolationSpeed * deltaTime);
            var current = body2D.position;
            var next = Vector2.Lerp(current, (Vector2)targetPosition, t);
            body2D.MovePosition(next);
        }

        private static byte[] BuildPayload(int networkId, byte flags, Vector3 position, Quaternion rotation)
        {
            var payloadSize = 5;
            if ((flags & PositionFlag) != 0)
            {
                payloadSize += 12;
            }
            if ((flags & RotationFlag) != 0)
            {
                payloadSize += 16;
            }

            var payload = new byte[payloadSize];
            WriteInt32(payload, 0, networkId);
            payload[4] = flags;

            var offset = 5;
            if ((flags & PositionFlag) != 0)
            {
                WriteVector3(payload, offset, position);
                offset += 12;
            }
            if ((flags & RotationFlag) != 0)
            {
                WriteQuaternion(payload, offset, rotation);
                offset += 16;
            }

            return payload;
        }

        private static void WriteVector3(byte[] buffer, int offset, Vector3 value)
        {
            WriteSingle(buffer, offset, value.x);
            WriteSingle(buffer, offset + 4, value.y);
            WriteSingle(buffer, offset + 8, value.z);
        }

        private static void WriteQuaternion(byte[] buffer, int offset, Quaternion value)
        {
            WriteSingle(buffer, offset, value.x);
            WriteSingle(buffer, offset + 4, value.y);
            WriteSingle(buffer, offset + 8, value.z);
            WriteSingle(buffer, offset + 12, value.w);
        }

        private static void WriteSingle(byte[] buffer, int offset, float value)
        {
            WriteInt32(buffer, offset, BitConverter.SingleToInt32Bits(value));
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }
    }
}
