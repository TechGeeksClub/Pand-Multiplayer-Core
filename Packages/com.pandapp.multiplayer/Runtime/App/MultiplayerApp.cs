using Pandapp.Multiplayer.Core;
using System;
using UnityEngine;

namespace Pandapp.Multiplayer.App
{
    public sealed class MultiplayerApp : MonoBehaviour
    {
        public delegate bool MessageInterceptor(NetworkMessage message);

        [Header("Config")]
        [SerializeField] private MultiplayerConfig config;

        [Header("Bindings")]
        [Tooltip("Required. Must implement INetworkTransport.")]
        [SerializeField] private MonoBehaviour transportBehaviour;
        [Tooltip("Optional. Must implement ISceneLoader. If omitted, a default loader is used.")]
        [SerializeField] private MonoBehaviour sceneLoaderBehaviour;
        [Tooltip("Optional. Must implement IGameModule (your game-specific module).")]
        [SerializeField] private MonoBehaviour gameModuleBehaviour;

        [Header("Lifecycle")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        public static MultiplayerApp Instance { get; private set; }

        public MultiplayerConfig Config => config;
        public INetworkTransport Transport { get; private set; }
        public ISessionService Session => sessionService;

        public event MessageInterceptor MessageInterceptors;

        private ISceneLoader sceneLoader;
        private IGameModule gameModule;
        private SessionService sessionService;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            Transport = ResolveTransport();
            if (Transport == null)
            {
                enabled = false;
                return;
            }

            sceneLoader = ResolveSceneLoader();
            gameModule = ResolveGameModule();

            sessionService = new SessionService(Transport);
            sessionService.GameStartRequested += HandleGameStartRequested;
            sessionService.Error += HandleSessionError;

            Transport.PlayerJoined += HandlePlayerJoined;
            Transport.PlayerLeft += HandlePlayerLeft;
            Transport.MessageReceived += HandleMessageReceived;
        }

        private void Update()
        {
            gameModule?.OnUpdate(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (Transport != null)
            {
                Transport.PlayerJoined -= HandlePlayerJoined;
                Transport.PlayerLeft -= HandlePlayerLeft;
                Transport.MessageReceived -= HandleMessageReceived;
            }

            if (sessionService != null)
            {
                sessionService.GameStartRequested -= HandleGameStartRequested;
                sessionService.Error -= HandleSessionError;
                sessionService.Dispose();
            }
        }

        public void Connect(ConnectOptions options) => sessionService?.Connect(options);
        public void Disconnect() => sessionService?.Disconnect();

        public void CreateRoom(RoomOptions options) => sessionService?.CreateRoom(options);
        public void JoinRoom(string roomCode) => sessionService?.JoinRoom(roomCode);
        public void QuickMatch(QuickMatchOptions options) => sessionService?.QuickMatch(options);
        public void LeaveRoom() => sessionService?.LeaveRoom();

        public void SetReady(bool ready) => sessionService?.SetReady(ready);

        public void RequestStart(SceneId sceneId) => sessionService?.RequestStart(sceneId);

        public void RequestStartConfiguredGame()
        {
            if (config == null)
            {
                LogError("MultiplayerConfig is not assigned.");
                return;
            }

            RequestStart(config.GameScene);
        }

        private INetworkTransport ResolveTransport()
        {
            if (transportBehaviour == null)
            {
                LogError("Transport behaviour is not assigned.");
                return null;
            }

            if (transportBehaviour is INetworkTransport transport)
            {
                return transport;
            }

            LogError(
                $"Transport behaviour does not implement {nameof(INetworkTransport)}: {transportBehaviour.GetType().FullName}",
                transportBehaviour);
            return null;
        }

        private ISceneLoader ResolveSceneLoader()
        {
            if (sceneLoaderBehaviour != null)
            {
                if (sceneLoaderBehaviour is ISceneLoader loader)
                {
                    return loader;
                }

                LogError(
                    $"SceneLoader behaviour does not implement {nameof(ISceneLoader)}: {sceneLoaderBehaviour.GetType().FullName}",
                    sceneLoaderBehaviour);
            }

            return new UnitySceneLoader(config != null ? config.SceneCatalog : null);
        }

        private IGameModule ResolveGameModule()
        {
            if (gameModuleBehaviour == null)
            {
                return null;
            }

            if (gameModuleBehaviour is IGameModule module)
            {
                return module;
            }

            var candidateCount = 0;
            MonoBehaviour candidateBehaviour = null;
            IGameModule candidateModule = null;
            string candidateTypeNames = null;

            var behaviours = gameModuleBehaviour.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || ReferenceEquals(behaviour, gameModuleBehaviour))
                {
                    continue;
                }

                if (!(behaviour is IGameModule moduleBehaviour))
                {
                    continue;
                }

                candidateCount++;
                candidateTypeNames = string.IsNullOrEmpty(candidateTypeNames)
                    ? behaviour.GetType().FullName
                    : candidateTypeNames + ", " + behaviour.GetType().FullName;

                if (candidateModule == null)
                {
                    candidateBehaviour = behaviour;
                    candidateModule = moduleBehaviour;
                }
            }

            if (candidateCount == 1 && candidateModule != null)
            {
                LogWarning(
                    $"GameModule behaviour does not implement {nameof(IGameModule)}: {gameModuleBehaviour.GetType().FullName}. " +
                    $"Using {candidateBehaviour.GetType().FullName} found on the same GameObject. " +
                    "Assign the correct component to silence this warning.",
                    candidateBehaviour);
                return candidateModule;
            }

            if (candidateCount > 1)
            {
                LogError(
                    $"GameModule behaviour does not implement {nameof(IGameModule)}: {gameModuleBehaviour.GetType().FullName}. " +
                    $"Multiple {nameof(IGameModule)} components found on the same GameObject: {candidateTypeNames}. " +
                    "Assign the correct one in the inspector.",
                    gameModuleBehaviour);
                return null;
            }

            LogError(
                $"GameModule behaviour does not implement {nameof(IGameModule)}: {gameModuleBehaviour.GetType().FullName}. " +
                $"Assign a component implementing {nameof(IGameModule)}.",
                gameModuleBehaviour);
            return null;
        }

        private void HandleGameStartRequested(SceneId sceneId)
        {
            sceneLoader?.Load(sceneId);
        }

        private void HandleSessionError(TransportError error)
        {
            LogError(error.ToString());
        }

        private void HandlePlayerJoined(PlayerInfo player)
        {
            gameModule?.OnPlayerJoined(player);
        }

        private void HandlePlayerLeft(PlayerInfo player)
        {
            gameModule?.OnPlayerLeft(player);
        }

        private void HandleMessageReceived(NetworkMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (CoreMessageIds.IsCoreMessage(message.MessageId))
            {
                return;
            }

            if (IsMessageIntercepted(message))
            {
                return;
            }

            gameModule?.OnMessageReceived(message);
        }

        private bool IsMessageIntercepted(NetworkMessage message)
        {
            if (MessageInterceptors == null)
            {
                return false;
            }

            var invocationList = MessageInterceptors.GetInvocationList();
            for (var i = 0; i < invocationList.Length; i++)
            {
                var interceptor = (MessageInterceptor)invocationList[i];
                try
                {
                    if (interceptor(message))
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

        private void LogError(string message, UnityEngine.Object context = null)
        {
            Debug.LogError($"[{nameof(MultiplayerApp)}] {message}", context != null ? context : this);
        }

        private void LogWarning(string message, UnityEngine.Object context = null)
        {
            Debug.LogWarning($"[{nameof(MultiplayerApp)}] {message}", context != null ? context : this);
        }
    }
}
