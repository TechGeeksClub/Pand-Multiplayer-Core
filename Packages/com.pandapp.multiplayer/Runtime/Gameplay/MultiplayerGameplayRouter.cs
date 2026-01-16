using System;
using System.Collections.Generic;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerGameplayRouter : MonoBehaviour
    {
        private MultiplayerApp app;
        private readonly List<IGameplayMessageHandler> handlers = new List<IGameplayMessageHandler>();

        public static void EnsureForApp(MultiplayerApp app)
        {
            if (app == null)
            {
                return;
            }

            if (app.GetComponent<MultiplayerGameplayRouter>() != null)
            {
                return;
            }

            app.gameObject.AddComponent<MultiplayerGameplayRouter>();
        }

        private void Awake()
        {
            app = GetComponent<MultiplayerApp>();
            if (app == null)
            {
                app = MultiplayerApp.Instance;
            }
        }

        private void OnEnable()
        {
            if (app == null)
            {
                return;
            }

            app.MessageInterceptors += HandleMessage;
        }

        private void OnDisable()
        {
            if (app == null)
            {
                return;
            }

            app.MessageInterceptors -= HandleMessage;
        }

        public void RegisterHandler(IGameplayMessageHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }

        public void UnregisterHandler(IGameplayMessageHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            handlers.Remove(handler);
        }

        private bool HandleMessage(NetworkMessage message)
        {
            if (message == null)
            {
                return false;
            }

            if (!GameplayMessageIds.IsGameplayMessage(message.MessageId))
            {
                return false;
            }

            if (message.MessageId == GameplayMessageIds.TransformState)
            {
                HandleTransformState(message);
                return true;
            }

            if (message.MessageId == GameplayMessageIds.Rigidbody2DState)
            {
                HandleRigidbody2DState(message);
                return true;
            }

            if (!DispatchToHandlers(message))
            {
                Debug.LogWarning($"[{nameof(MultiplayerGameplayRouter)}] Unhandled gameplay message id: {message.MessageId}", this);
            }

            return true;
        }

        private bool DispatchToHandlers(NetworkMessage message)
        {
            if (handlers.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    if (handler.HandleMessage(message))
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            return false;
        }

        private static void HandleTransformState(NetworkMessage message)
        {
            var payload = message.Payload;
            if (payload == null || payload.Length < 5)
            {
                return;
            }

            var networkId = BitConverter.ToInt32(payload, 0);
            var flags = payload[4];

            if (!NetworkObjectRegistry.TryGet(networkId, out var identity) || identity == null)
            {
                return;
            }

            if (!identity.TryGetComponent<NetworkTransformSync>(out var sync) || sync == null)
            {
                return;
            }

            var offset = 5;
            Vector3? position = null;
            Quaternion? rotation = null;

            if ((flags & NetworkTransformSync.PositionFlag) != 0)
            {
                if (payload.Length < offset + 12)
                {
                    return;
                }

                position = ReadVector3(payload, offset);
                offset += 12;
            }

            if ((flags & NetworkTransformSync.RotationFlag) != 0)
            {
                if (payload.Length < offset + 16)
                {
                    return;
                }

                rotation = ReadQuaternion(payload, offset);
                offset += 16;
            }

            sync.ApplyRemoteState(position, rotation, message.SenderId);
        }

        private static void HandleRigidbody2DState(NetworkMessage message)
        {
            var payload = message.Payload;
            if (payload == null || payload.Length < 24)
            {
                return;
            }

            var networkId = BitConverter.ToInt32(payload, 0);
            var sequence = BitConverter.ToInt32(payload, 4);
            if (!NetworkObjectRegistry.TryGet(networkId, out var identity) || identity == null)
            {
                return;
            }

            if (!identity.TryGetComponent<NetworkRigidbody2DSync>(out var sync) || sync == null)
            {
                return;
            }

            var position = ReadVector2(payload, 8);
            var velocity = ReadVector2(payload, 16);

            sync.ApplyRemoteState(position, velocity, sequence, message.SenderId);
        }

        private static Vector3 ReadVector3(byte[] buffer, int offset)
        {
            return new Vector3(
                BitConverter.ToSingle(buffer, offset),
                BitConverter.ToSingle(buffer, offset + 4),
                BitConverter.ToSingle(buffer, offset + 8));
        }

        private static Vector2 ReadVector2(byte[] buffer, int offset)
        {
            return new Vector2(
                BitConverter.ToSingle(buffer, offset),
                BitConverter.ToSingle(buffer, offset + 4));
        }

        private static Quaternion ReadQuaternion(byte[] buffer, int offset)
        {
            return new Quaternion(
                BitConverter.ToSingle(buffer, offset),
                BitConverter.ToSingle(buffer, offset + 4),
                BitConverter.ToSingle(buffer, offset + 8),
                BitConverter.ToSingle(buffer, offset + 12));
        }
    }
}
