using System;

namespace Pandapp.Multiplayer.Monetization
{
    public interface IAdsService
    {
        AdsProvider Provider { get; }
        bool IsInitialized { get; }

        void Initialize(MonetizationSettings settings);

        bool IsRewardedReady(string placementId);
        bool IsInterstitialReady(string placementId);

        void ShowRewarded(string placementId, Action<AdsShowResult> callback);
        void ShowInterstitial(string placementId, Action<AdsShowResult> callback);

        void ShowBanner(string placementId);
        void HideBanner(string placementId);
    }
}
