using System;
using Pandapp.Multiplayer.App;
using Pandapp.Multiplayer.Core;
using Pandapp.Multiplayer.Gameplay;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Pandapp.Multiplayer.Samples.MinimalDemo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkTransformSync))]
    public sealed class MinimalDemoMover : MonoBehaviour
    {
        [Min(0f)]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Input")]
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode forwardKey = KeyCode.W;
        [SerializeField] private KeyCode backKey = KeyCode.S;

        [Header("Debug")]
        [SerializeField] private Color localColor = new Color(0.2f, 0.9f, 0.2f, 1f);
        [SerializeField] private Color remoteColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        private NetworkTransformSync sync;
        private Renderer cachedRenderer;
        private bool colorApplied;

        public void SetKeys(KeyCode left, KeyCode right, KeyCode forward, KeyCode back)
        {
            leftKey = left;
            rightKey = right;
            forwardKey = forward;
            backKey = back;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = Mathf.Max(0f, speed);
        }

        public void SetColors(Color local, Color remote)
        {
            localColor = local;
            remoteColor = remote;
        }

        private void Awake()
        {
            sync = GetComponent<NetworkTransformSync>();
            cachedRenderer = GetComponentInChildren<Renderer>();
        }

        private void Start()
        {
            ApplyColorIfPossible();
        }

        private void Update()
        {
            if (!HasLocalAuthority())
            {
                if (!colorApplied)
                {
                    ApplyColorIfPossible();
                }
                return;
            }

            ApplyColorIfPossible();

            var direction = ReadDirection();
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            direction = direction.normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
        }

        private Vector3 ReadDirection()
        {
            var direction = Vector3.zero;

            if (GetKey(leftKey))
            {
                direction.x -= 1f;
            }

            if (GetKey(rightKey))
            {
                direction.x += 1f;
            }

            if (GetKey(forwardKey))
            {
                direction.z += 1f;
            }

            if (GetKey(backKey))
            {
                direction.z -= 1f;
            }

            return direction;
        }

        private static bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            switch (key)
            {
                case KeyCode.A: return keyboard.aKey.isPressed;
                case KeyCode.D: return keyboard.dKey.isPressed;
                case KeyCode.W: return keyboard.wKey.isPressed;
                case KeyCode.S: return keyboard.sKey.isPressed;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey.isPressed;
                case KeyCode.RightArrow: return keyboard.rightArrowKey.isPressed;
                case KeyCode.UpArrow: return keyboard.upArrowKey.isPressed;
                case KeyCode.DownArrow: return keyboard.downArrowKey.isPressed;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        private bool HasLocalAuthority()
        {
            var app = MultiplayerApp.Instance;
            if (app == null || sync == null)
            {
                return false;
            }

            var transport = app.Transport;
            if (transport == null || transport.RoomState != TransportRoomState.InRoom)
            {
                return false;
            }

            switch (sync.Authority)
            {
                case NetworkTransformSync.AuthorityMode.Host:
                    return app.Session != null && app.Session.IsHost;

                case NetworkTransformSync.AuthorityMode.Owner:
                {
                    var localPlayerId = transport.LocalPlayerId;
                    return !string.IsNullOrEmpty(localPlayerId)
                        && !string.IsNullOrEmpty(sync.OwnerPlayerId)
                        && string.Equals(localPlayerId, sync.OwnerPlayerId, StringComparison.Ordinal);
                }

                default:
                    return false;
            }
        }

        private void ApplyColorIfPossible()
        {
            if (cachedRenderer == null)
            {
                colorApplied = true;
                return;
            }

            cachedRenderer.material.color = HasLocalAuthority() ? localColor : remoteColor;
            colorApplied = true;
        }
    }
}
