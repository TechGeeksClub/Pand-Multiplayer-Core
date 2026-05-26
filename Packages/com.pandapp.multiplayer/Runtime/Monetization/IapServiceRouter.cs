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

        public bool HasReceipt(string productId)
        {
            return activeService is IIapReceiptService receiptService
                && receiptService.HasReceipt(productId);
        }

        public bool TryGetLocalizedPriceString(string productId, out string price)
        {
            if (activeService is IIapProductMetadataService metadataService)
            {
                return metadataService.TryGetLocalizedPriceString(productId, out price);
            }

            price = string.Empty;
            return false;
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
