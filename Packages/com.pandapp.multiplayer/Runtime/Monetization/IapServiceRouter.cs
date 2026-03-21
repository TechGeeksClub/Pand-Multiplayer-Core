using UnityEngine;

namespace Pandapp.Multiplayer.Monetization
{
    public sealed class IapServiceRouter : MonoBehaviour, IIapService
    {
        [SerializeField] private MonetizationSettings settings;
        [SerializeField] private MonoBehaviour unityIapService;
        [SerializeField] private bool autoInitialize = true;

        private IIapService activeService;

        public IapProvider Provider => activeService != null ? activeService.Provider : IapProvider.None;
        public bool IsInitialized => activeService != null && activeService.IsInitialized;

        private void Awake()
        {
            activeService = ResolveService();
            if (autoInitialize && activeService != null)
            {
                activeService.Initialize(settings);
            }
        }

        public void Initialize(MonetizationSettings monetizationSettings)
        {
            settings = monetizationSettings;
            activeService = ResolveService();
            activeService?.Initialize(settings);
        }

        public void Purchase(string productId, System.Action<IapPurchaseResult> callback)
        {
            if (activeService == null)
            {
                callback?.Invoke(IapPurchaseResult.Failed);
                return;
            }

            activeService.Purchase(productId, callback);
        }

        public void RestorePurchases(System.Action<bool> callback)
        {
            if (activeService == null)
            {
                callback?.Invoke(false);
                return;
            }

            activeService.RestorePurchases(callback);
        }

        private IIapService ResolveService()
        {
            if (settings == null || settings.iapProvider == IapProvider.None)
            {
                return null;
            }

            return unityIapService as IIapService;
        }
    }
}
