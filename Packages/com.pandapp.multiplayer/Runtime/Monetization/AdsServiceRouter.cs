using UnityEngine;

namespace Pandapp.Multiplayer.Monetization
{
    public sealed class AdsServiceRouter : MonoBehaviour, IAdsService
    {
        [SerializeField] private MonetizationSettings settings;
        [SerializeField] private MonoBehaviour unityAdsService;
        [SerializeField] private MonoBehaviour adMobAdsService;
        [SerializeField] private bool autoInitialize = true;

        private IAdsService activeService;

        public AdsProvider Provider => activeService != null ? activeService.Provider : AdsProvider.None;
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

        public bool IsRewardedReady(string placementId)
        {
            return activeService != null && activeService.IsRewardedReady(placementId);
        }

        public bool IsInterstitialReady(string placementId)
        {
            return activeService != null && activeService.IsInterstitialReady(placementId);
        }

        public void ShowRewarded(string placementId, System.Action<AdsShowResult> callback)
        {
            if (activeService == null)
            {
                callback?.Invoke(AdsShowResult.Failed);
                return;
            }

            activeService.ShowRewarded(placementId, callback);
        }

        public void ShowInterstitial(string placementId, System.Action<AdsShowResult> callback)
        {
            if (activeService == null)
            {
                callback?.Invoke(AdsShowResult.Failed);
                return;
            }

            activeService.ShowInterstitial(placementId, callback);
        }

        public void ShowBanner(string placementId)
        {
            activeService?.ShowBanner(placementId);
        }

        public void HideBanner(string placementId)
        {
            activeService?.HideBanner(placementId);
        }

        private IAdsService ResolveService()
        {
            if (settings == null)
            {
                return null;
            }

            var unity = unityAdsService as IAdsService;
            var admob = adMobAdsService as IAdsService;

            switch (settings.adsProvider)
            {
                case AdsProvider.UnityAds:
                    return unity;
                case AdsProvider.AdMob:
                    return admob;
                default:
                    return null;
            }
        }
    }
}
