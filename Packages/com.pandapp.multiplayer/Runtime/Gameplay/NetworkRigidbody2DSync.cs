using System;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class NetworkRigidbody2DSync : MonoBehaviour
    {
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
        [SerializeField] private float velocityThreshold = 0.05f;

        [Header("Receive")]
        [Min(0f)]
        [SerializeField] private float positionCorrectionSpeed = 20f;
        [Min(0f)]
        [SerializeField] private float velocityCorrectionSpeed = 20f;
        [SerializeField] private bool extrapolate = true;
        [Tooltip("When enabled, remote instances are driven as kinematic proxies (MovePosition). When disabled, remote instances stay dynamic and get reconciled via velocity correction.")]
        [SerializeField] private bool forceKinematicWhenRemote = true;
        [Min(0f)]
        [SerializeField] private float maxPositionCorrectionSpeed = 35f;
        [Min(0f)]
        [SerializeField] private float snapDistance = 0.75f;
        [Range(0f, 1f)]
        [Tooltip("Scales how much of the position-correction is allowed perpendicular to the authoritative velocity. Lower values reduce 'S' shaped corrections on fast moving objects but may take longer to converge.")]
        [SerializeField] private float perpendicularPositionCorrectionScale = 0.25f;

        [Header("Prediction (Optional)")]
        [Tooltip("When local non-authoritative instances run client-side physics (e.g. during collisions), reconciliation can be temporarily reduced to avoid jitter/rubber-banding.")]
        [Min(0f)]
        [SerializeField] private float predictionHoldSeconds = 0.12f;
        [Range(0f, 1f)]
        [Tooltip("Lower bound for correction scaling during prediction hold. 0 disables correction fully (can cause divergence), 1 keeps full correction (less smoothing).")]
        [SerializeField] private float predictionHoldMinCorrectionScale = 0.25f;

        private NetworkIdentity identity;
        private Rigidbody2D body;

        private float sendTimer;
        private Vector2 lastSentPosition;
        private Vector2 lastSentVelocity;
        private bool hasSentOnce;
        private int sendSequence;

        private int lastReceivedSequence;
        private bool hasReceivedSequence;

        private Vector2 networkPosition;
        private Vector2 networkVelocity;
        private bool hasNetworkState;

        private Vector2 predictedPosition;
        private Vector2 predictedVelocity;
        private bool hasPredictedState;

        private float predictionHoldRemainingSeconds;
        private float predictionHoldTotalSeconds;

        public AuthorityMode Authority => authority;
        public string OwnerPlayerId => ownerPlayerId;
        public bool ForceKinematicWhenRemote => forceKinematicWhenRemote;

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
            ResetSendState();
        }

        public void SetSendSettings(float intervalSeconds, float positionThresholdValue, float velocityThresholdValue)
        {
            intervalSeconds = Mathf.Max(0f, intervalSeconds);
            positionThresholdValue = Mathf.Max(0f, positionThresholdValue);
            velocityThresholdValue = Mathf.Max(0f, velocityThresholdValue);

            if (Mathf.Approximately(sendIntervalSeconds, intervalSeconds)
                && Mathf.Approximately(positionThreshold, positionThresholdValue)
                && Mathf.Approximately(velocityThreshold, velocityThresholdValue))
            {
                return;
            }

            sendIntervalSeconds = intervalSeconds;
            positionThreshold = positionThresholdValue;
            velocityThreshold = velocityThresholdValue;
            ResetSendState();
        }

        public void SetReceiveSettings(float positionCorrectionSpeedValue, float velocityCorrectionSpeedValue, bool extrapolateValue)
        {
            positionCorrectionSpeed = Mathf.Max(0f, positionCorrectionSpeedValue);
            velocityCorrectionSpeed = Mathf.Max(0f, velocityCorrectionSpeedValue);
            extrapolate = extrapolateValue;
        }

        public void SetRemoteMode(bool kinematicProxy)
        {
            forceKinematicWhenRemote = kinematicProxy;
        }

        public void SetReconcileSettings(float maxPositionCorrectionSpeedValue, float snapDistanceValue)
        {
            maxPositionCorrectionSpeed = Mathf.Max(0f, maxPositionCorrectionSpeedValue);
            snapDistance = Mathf.Max(0f, snapDistanceValue);
        }

        public void SetPerpendicularPositionCorrectionScale(float value)
        {
            perpendicularPositionCorrectionScale = Mathf.Clamp01(value);
        }

        public void SetPredictionHoldSettings(float holdSeconds, float minCorrectionScale)
        {
            predictionHoldSeconds = Mathf.Max(0f, holdSeconds);
            predictionHoldMinCorrectionScale = Mathf.Clamp01(minCorrectionScale);
        }

        public void AddPredictedImpulse(Vector2 impulse)
        {
            if (IsLocalAuthority())
            {
                return;
            }

            if (!hasPredictedState)
            {
                predictedPosition = body != null ? body.position : (Vector2)transform.position;
                predictedVelocity = Vector2.zero;
                hasPredictedState = true;
            }

            var mass = body != null && body.mass > 0f ? body.mass : 1f;
            predictedVelocity += impulse / mass;

            if (predictionHoldSeconds > 0f)
            {
                predictionHoldTotalSeconds = Mathf.Max(predictionHoldTotalSeconds, predictionHoldSeconds);
                predictionHoldRemainingSeconds = Mathf.Max(predictionHoldRemainingSeconds, predictionHoldSeconds);
            }
        }

        public void NotifyLocalPredictedPhysics()
        {
            NotifyLocalPredictedPhysics(predictionHoldSeconds);
        }

        public void NotifyLocalPredictedPhysics(float holdSeconds)
        {
            if (holdSeconds <= 0f)
            {
                return;
            }

            if (IsLocalAuthority() || body == null)
            {
                return;
            }

            predictedPosition = body.position;
            predictedVelocity = body.linearVelocity;
            hasPredictedState = true;

            predictionHoldTotalSeconds = Mathf.Max(predictionHoldTotalSeconds, holdSeconds);
            predictionHoldRemainingSeconds = Mathf.Max(predictionHoldRemainingSeconds, holdSeconds);
        }

        private void Awake()
        {
            identity = GetComponent<NetworkIdentity>();
            body = GetComponent<Rigidbody2D>();
        }

        private void OnEnable()
        {
            MultiplayerGameplayRouter.EnsureForApp(MultiplayerApp.Instance);
            ResetRemoteState();
        }

        private void FixedUpdate()
        {
            if (!IsReadyForGameplay())
            {
                return;
            }

            if (IsLocalAuthority())
            {
                sendTimer += Time.fixedDeltaTime;
                if (sendTimer >= sendIntervalSeconds)
                {
                    sendTimer = 0f;
                    TrySendState();
                }

                return;
            }

            if (hasNetworkState)
            {
                ApplyRemote(Time.fixedDeltaTime);
            }
        }

        internal void ApplyRemoteState(Vector2 position, Vector2 velocity, int sequence, string senderId)
        {
            if (!IsReadyForGameplay())
            {
                return;
            }

            if (IsLocalAuthority())
            {
                return;
            }

            if (hasReceivedSequence && sequence <= lastReceivedSequence)
            {
                return;
            }

            hasReceivedSequence = true;
            lastReceivedSequence = sequence;

            networkPosition = position;
            networkVelocity = velocity;
            hasNetworkState = true;

            if (!hasPredictedState)
            {
                predictedPosition = position;
                predictedVelocity = velocity;
                hasPredictedState = true;

                if (body != null)
                {
                    body.position = position;
                    body.linearVelocity = forceKinematicWhenRemote ? Vector2.zero : velocity;
                    body.angularVelocity = 0f;
                }
            }
        }

        private void ApplyRemote(float deltaTime)
        {
            if (body == null)
            {
                return;
            }

            if (extrapolate)
            {
                predictedPosition += predictedVelocity * deltaTime;
            }

            var positionT = DampToLerp(positionCorrectionSpeed, deltaTime);
            var velocityT = DampToLerp(velocityCorrectionSpeed, deltaTime);

            var holdT = 0f;
            if (predictionHoldRemainingSeconds > 0f)
            {
                predictedPosition = body.position;
                predictedVelocity = body.linearVelocity;

                predictionHoldRemainingSeconds = Mathf.Max(0f, predictionHoldRemainingSeconds - deltaTime);
                holdT = predictionHoldTotalSeconds > 0f
                    ? Mathf.Clamp01(predictionHoldRemainingSeconds / predictionHoldTotalSeconds)
                    : 0f;

                var correctionScale = Mathf.Lerp(1f, Mathf.Clamp01(predictionHoldMinCorrectionScale), holdT);
                positionT *= correctionScale;
                velocityT *= correctionScale;

                if (predictionHoldRemainingSeconds <= 0f)
                {
                    predictionHoldTotalSeconds = 0f;
                }
            }

            predictedPosition = Vector2.Lerp(predictedPosition, networkPosition, positionT);
            predictedVelocity = Vector2.Lerp(predictedVelocity, networkVelocity, velocityT);

            if (forceKinematicWhenRemote)
            {
                if (body.bodyType != RigidbodyType2D.Kinematic)
                {
                    body.bodyType = RigidbodyType2D.Kinematic;
                }

                body.MovePosition(predictedPosition);
                return;
            }

            if (body.bodyType != RigidbodyType2D.Dynamic)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
            }

            ApplyDynamicReconcile(predictedPosition, predictedVelocity, velocityT, deltaTime);
        }

        private void ApplyDynamicReconcile(Vector2 targetPosition, Vector2 targetVelocity, float velocityLerpT, float deltaTime)
        {
            if (body == null)
            {
                return;
            }

            var currentPosition = body.position;
            var positionError = targetPosition - currentPosition;

            if (predictionHoldRemainingSeconds <= 0f
                && snapDistance > 0f
                && positionError.sqrMagnitude > snapDistance * snapDistance)
            {
                body.position = targetPosition;
                body.linearVelocity = targetVelocity;
                body.angularVelocity = 0f;
                return;
            }

            var correctionVelocity = positionError * positionCorrectionSpeed;

            if (perpendicularPositionCorrectionScale < 1f)
            {
                var dir = targetVelocity;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    dir = dir.normalized;
                    var along = Vector2.Dot(correctionVelocity, dir);
                    var parallel = dir * along;
                    var perpendicular = correctionVelocity - parallel;
                    correctionVelocity = parallel + perpendicular * Mathf.Clamp01(perpendicularPositionCorrectionScale);
                }
            }

            if (deltaTime > 0.0001f)
            {
                var maxStepSpeed = positionError.magnitude / deltaTime;
                correctionVelocity = Vector2.ClampMagnitude(correctionVelocity, maxStepSpeed);
            }

            if (maxPositionCorrectionSpeed > 0f)
            {
                correctionVelocity = Vector2.ClampMagnitude(correctionVelocity, maxPositionCorrectionSpeed);
            }

            var desiredVelocity = targetVelocity + correctionVelocity;
            body.linearVelocity = Vector2.Lerp(body.linearVelocity, desiredVelocity, velocityLerpT);
        }

        private void TrySendState()
        {
            var app = MultiplayerApp.Instance;
            var transport = app != null ? app.Transport : null;
            if (transport == null)
            {
                return;
            }

            var pos = body.position;
            var vel = body.linearVelocity;

            if (!ShouldSend(pos, vel))
            {
                return;
            }

            lastSentPosition = pos;
            lastSentVelocity = vel;
            hasSentOnce = true;
            sendSequence++;

            transport.Send(
                new NetworkMessage(GameplayMessageIds.Rigidbody2DState, BuildPayload(identity.NetworkId, sendSequence, pos, vel)),
                SendOptions.ToOthers(reliable: false));
        }

        private bool ShouldSend(Vector2 pos, Vector2 vel)
        {
            if (!hasSentOnce)
            {
                return true;
            }

            var posThresholdSqr = positionThreshold * positionThreshold;
            if ((pos - lastSentPosition).sqrMagnitude >= posThresholdSqr)
            {
                return true;
            }

            var velThresholdSqr = velocityThreshold * velocityThreshold;
            return (vel - lastSentVelocity).sqrMagnitude >= velThresholdSqr;
        }

        private void ResetSendState()
        {
            sendTimer = 0f;
            hasSentOnce = false;
        }

        private void ResetRemoteState()
        {
            hasNetworkState = false;
            hasPredictedState = false;
            hasReceivedSequence = false;
            predictedVelocity = Vector2.zero;
            predictionHoldRemainingSeconds = 0f;
            predictionHoldTotalSeconds = 0f;
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

        private static float DampToLerp(float speed, float deltaTime)
        {
            if (speed <= 0f)
            {
                return 1f;
            }

            return 1f - Mathf.Exp(-speed * deltaTime);
        }

        private static byte[] BuildPayload(int networkId, int sequence, Vector2 position, Vector2 velocity)
        {
            var payload = new byte[24];
            WriteInt32(payload, 0, networkId);
            WriteInt32(payload, 4, sequence);
            WriteVector2(payload, 8, position);
            WriteVector2(payload, 16, velocity);
            return payload;
        }

        private static void WriteVector2(byte[] buffer, int offset, Vector2 value)
        {
            WriteSingle(buffer, offset, value.x);
            WriteSingle(buffer, offset + 4, value.y);
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
