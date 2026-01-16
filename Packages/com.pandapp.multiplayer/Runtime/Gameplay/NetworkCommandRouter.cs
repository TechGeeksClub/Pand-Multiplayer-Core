using System;
using System.Collections.Generic;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NetworkCommandRouter : MonoBehaviour, IGameplayMessageHandler
    {
        [SerializeField] private MultiplayerApp app;

        private MultiplayerGameplayRouter router;
        private readonly List<INetworkCommandHandler> handlers = new List<INetworkCommandHandler>();

        public static void EnsureForApp(MultiplayerApp app)
        {
            if (app == null)
            {
                return;
            }

            if (app.GetComponent<NetworkCommandRouter>() != null)
            {
                return;
            }

            app.gameObject.AddComponent<NetworkCommandRouter>();
        }

        private void Awake()
        {
            if (app == null)
            {
                app = GetComponent<MultiplayerApp>() ?? MultiplayerApp.Instance;
            }
        }

        private void OnEnable()
        {
            if (app == null)
            {
                app = MultiplayerApp.Instance;
            }

            if (app == null)
            {
                Debug.LogError($"[{nameof(NetworkCommandRouter)}] MultiplayerApp not found.", this);
                enabled = false;
                return;
            }

            MultiplayerGameplayRouter.EnsureForApp(app);
            router = app.GetComponent<MultiplayerGameplayRouter>();
            router?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            if (router != null)
            {
                router.UnregisterHandler(this);
                router = null;
            }
        }

        public void RegisterHandler(INetworkCommandHandler handler)
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

        public void UnregisterHandler(INetworkCommandHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            handlers.Remove(handler);
        }

        public bool SendToHost(ushort commandId, int targetNetworkId, byte[] payload = null, bool reliable = true)
        {
            if (!IsInRoom(out var transport))
            {
                Debug.LogError($"[{nameof(NetworkCommandRouter)}] Not in room.", this);
                return false;
            }

            var localPlayerId = transport.LocalPlayerId;
            if (string.IsNullOrEmpty(localPlayerId))
            {
                Debug.LogError($"[{nameof(NetworkCommandRouter)}] LocalPlayerId is missing.", this);
                return false;
            }

            if (IsLocalHost())
            {
                var decodedPayload = payload == null || payload.Length == 0
                    ? new ArraySegment<byte>(Array.Empty<byte>())
                    : new ArraySegment<byte>(payload);

                return DispatchCommand(commandId, targetNetworkId, localPlayerId, decodedPayload);
            }

            var message = new NetworkMessage(
                GameplayMessageIds.Command,
                NetworkCommandCodec.WritePayload(commandId, targetNetworkId, payload),
                localPlayerId);

            transport.Send(message, SendOptions.ToHost(reliable));
            return true;
        }

        public bool HandleMessage(NetworkMessage message)
        {
            if (message == null)
            {
                return false;
            }

            if (message.MessageId != GameplayMessageIds.Command)
            {
                return false;
            }

            if (!NetworkCommandCodec.TryReadPayload(message.Payload, out var commandId, out var targetNetworkId, out var payload))
            {
                Debug.LogWarning($"[{nameof(NetworkCommandRouter)}] Invalid command payload.", this);
                return true;
            }

            if (!IsLocalHost())
            {
                return true;
            }

            var senderId = message.SenderId ?? string.Empty;
            if (string.IsNullOrEmpty(senderId))
            {
                Debug.LogWarning($"[{nameof(NetworkCommandRouter)}] Command missing SenderId.", this);
                return true;
            }

            if (!DispatchCommand(commandId, targetNetworkId, senderId, payload))
            {
                Debug.LogWarning(
                    $"[{nameof(NetworkCommandRouter)}] Unhandled command {commandId} (target={targetNetworkId}, sender={senderId}).",
                    this);
            }

            return true;
        }

        private bool DispatchCommand(ushort commandId, int targetNetworkId, string senderId, ArraySegment<byte> payload)
        {
            if (handlers.Count == 0)
            {
                return false;
            }

            var target = ResolveTarget(targetNetworkId);
            var context = new NetworkCommandContext(commandId, targetNetworkId, target, senderId, payload);

            for (var i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                if (handler == null)
                {
                    continue;
                }

                try
                {
                    if (handler.HandleCommand(context))
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

        private static NetworkIdentity ResolveTarget(int targetNetworkId)
        {
            if (targetNetworkId <= 0)
            {
                return null;
            }

            return NetworkObjectRegistry.TryGet(targetNetworkId, out var identity) ? identity : null;
        }

        private bool IsInRoom(out INetworkTransport transport)
        {
            transport = app != null ? app.Transport : null;
            return transport != null && transport.RoomState == TransportRoomState.InRoom;
        }

        private bool IsLocalHost()
        {
            return app != null && app.Session != null && app.Session.IsHost;
        }
    }
}

