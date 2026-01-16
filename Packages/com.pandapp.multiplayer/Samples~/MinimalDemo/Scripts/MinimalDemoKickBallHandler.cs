using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Gameplay;
using UnityEngine;

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    public sealed class MinimalDemoKickBallHandler : MonoBehaviour, INetworkCommandHandler
    {
        public const ushort KickBallCommandId = 1;

        private const int PlayerPrefabId = 1;

        [Header("Kick Settings")]
        [Min(0f)]
        [SerializeField] private float maxKickDistance = 2.25f;
        [Min(0f)]
        [SerializeField] private float kickDistance = 2f;

        private NetworkSpawner spawner;

        private void OnEnable()
        {
            var app = MultiplayerApp.Instance;
            if (app == null)
            {
                return;
            }

            NetworkCommandRouter.EnsureForApp(app);
            var router = app.GetComponent<NetworkCommandRouter>();
            router?.RegisterHandler(this);
        }

        private void OnDisable()
        {
            var app = MultiplayerApp.Instance;
            if (app == null)
            {
                return;
            }

            var router = app.GetComponent<NetworkCommandRouter>();
            router?.UnregisterHandler(this);
        }

        public bool HandleCommand(NetworkCommandContext context)
        {
            if (context.CommandId != KickBallCommandId)
            {
                return false;
            }

            if (context.Target == null)
            {
                return true;
            }

            if (!TryResolveSpawner(out var resolvedSpawner))
            {
                return true;
            }

            if (!resolvedSpawner.TryGetFirstSpawnedByOwner(context.SenderId, PlayerPrefabId, out var player) || player == null)
            {
                return true;
            }

            var ball = context.Target.transform;
            var playerTransform = player.transform;

            var delta = ball.position - playerTransform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude > maxKickDistance * maxKickDistance)
            {
                return true;
            }

            var direction = delta.sqrMagnitude < 0.0001f ? Vector3.forward : delta.normalized;
            var targetPosition = ball.position + direction * kickDistance;
            targetPosition.y = ball.position.y;

            ball.position = targetPosition;
            return true;
        }

        private bool TryResolveSpawner(out NetworkSpawner resolved)
        {
            resolved = spawner;
            if (resolved != null)
            {
                return true;
            }

            var app = MultiplayerApp.Instance;
            if (app == null)
            {
                return false;
            }

            spawner = app.GetComponent<NetworkSpawner>();
            resolved = spawner;
            return resolved != null;
        }
    }
}

