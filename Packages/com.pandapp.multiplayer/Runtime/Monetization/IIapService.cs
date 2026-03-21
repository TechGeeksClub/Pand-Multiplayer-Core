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
}
