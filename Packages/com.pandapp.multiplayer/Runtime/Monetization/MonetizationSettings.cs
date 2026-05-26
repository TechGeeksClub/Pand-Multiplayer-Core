using UnityEngine;
using UnityEngine.Purchasing;

namespace Pandapp.Multiplayer.Monetization
{
    [System.Serializable]
    public struct IapProductDefinition
    {
        public string productId;
        public ProductType productType;
        public int coinAmount;
        public bool grantsNoAds;
    }

    [CreateAssetMenu(menuName = "Pandapp/Monetization/Settings", fileName = "MonetizationSettings")]
    public sealed class MonetizationSettings : ScriptableObject
    {
        [Header("Providers")]
        public AdsProvider adsProvider = AdsProvider.None;
        public IapProvider iapProvider = IapProvider.None;

        [Header("Unity Ads")]
        public string unityAdsGameIdAndroid = string.Empty;
        public string unityAdsGameIdIos = string.Empty;
        public bool unityAdsTestMode = true;

        [Header("AdMob")]
        public string adMobAppIdAndroid = string.Empty;
        public string adMobAppIdIos = string.Empty;
        public bool adMobTestMode = true;

        [Header("Placements")]
        public string rewardedPlacementId = MonetizationConstants.DefaultRewardedPlacementId;
        public string interstitialPlacementId = MonetizationConstants.DefaultInterstitialPlacementId;
        public string bannerPlacementId = MonetizationConstants.DefaultBannerPlacementId;

        [Header("IAP Products")]
        public IapProductDefinition[] products = new IapProductDefinition[0];
        public string[] productIds = new string[0];

        public string GetUnityAdsGameId()
        {
#if UNITY_IOS
            return unityAdsGameIdIos;
#else
            return unityAdsGameIdAndroid;
#endif
        }

        public string GetAdMobAppId()
        {
#if UNITY_IOS
            return adMobAppIdIos;
#else
            return adMobAppIdAndroid;
#endif
        }
    }
}
