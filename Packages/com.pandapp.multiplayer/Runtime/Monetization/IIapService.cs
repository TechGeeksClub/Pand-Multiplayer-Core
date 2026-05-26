using System;

namespace Pandapp.Multiplayer.Monetization
{
    public interface IIapService
    {
        IapProvider Provider { get; }
        bool IsInitialized { get; }

        void Initialize(MonetizationSettings settings);
        void Purchase(string productId, Action<IapPurchaseResult> callback);
        void RestorePurchases(Action<bool> callback);
    }

    public interface IIapReceiptService
    {
        bool HasReceipt(string productId);
    }

    public interface IIapProductMetadataService
    {
        bool TryGetLocalizedPriceString(string productId, out string price);
    }
}
