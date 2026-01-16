using System;
using UnityEngine;

namespace Pandapp.Multiplayer.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class NetworkIdentity : MonoBehaviour
    {
        public enum IdentityKind
        {
            Scene = 0,
            Spawned = 1,
        }

        [SerializeField] private IdentityKind identityKind = IdentityKind.Scene;
        [SerializeField] private int networkId;

        public IdentityKind Kind => identityKind;
        public int NetworkId => networkId;

        public void SetKind(IdentityKind kind)
        {
            identityKind = kind;
        }

        private void OnEnable()
        {
            if (networkId <= 0)
            {
                if (identityKind == IdentityKind.Scene)
                {
                    Debug.LogError(
                        $"[{nameof(NetworkIdentity)}] NetworkId is not set on '{name}'. " +
                        "If this object is spawned at runtime, set Kind=Spawned on the prefab (NetworkId should stay 0 and will be assigned by NetworkSpawner).",
                        this);
                }
                return;
            }

            NetworkObjectRegistry.Register(this);
        }

        private void OnDisable()
        {
            if (networkId <= 0)
            {
                return;
            }

            NetworkObjectRegistry.Unregister(this);
        }

        public bool TrySetNetworkId(int value)
        {
            if (value <= 0)
            {
                Debug.LogError($"[{nameof(NetworkIdentity)}] Invalid NetworkId '{value}' on '{name}'.", this);
                return false;
            }

            if (networkId == value)
            {
                return true;
            }

            if (isActiveAndEnabled && networkId > 0)
            {
                NetworkObjectRegistry.Unregister(this);
            }

            networkId = value;

            if (isActiveAndEnabled)
            {
                NetworkObjectRegistry.Register(this);
            }

            return true;
        }

        [ContextMenu("Generate NetworkId")]
        private void GenerateNetworkId()
        {
            var guid = Guid.NewGuid();
            var value = guid.GetHashCode() & 0x7fffffff;
            if (value == 0)
            {
                value = 1;
            }

            networkId = value;
        }
    }
}
