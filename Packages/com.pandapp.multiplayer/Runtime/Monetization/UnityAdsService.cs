using System;
using UnityEngine;
#if UNITY_ADS
using UnityEngine.Advertisements;
#endif

namespace Pandapp.Multiplayer.Monetization
{
    public sealed class UnityAdsService : MonoBehaviour, IAdsService
#if UNITY_ADS
        , IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
#endif
    {
        private MonetizationSettings settings;
        private bool initialized;
        private bool initializationRequested;
        private bool bannerLoaded;
        private bool bannerShowRequested;
        private bool rewardedLoaded;
        private bool interstitialLoaded;

        private string rewardedPlacement;
        private string interstitialPlacement;
        private string bannerPlacement;

        private Action<AdsShowResult> rewardedCallback;
        private Action<AdsShowResult> interstitialCallback;

        public AdsProvider Provider => AdsProvider.UnityAds;
        public bool IsInitialized => initialized;

        public void Initialize(MonetizationSettings monetizationSettings)
        {
            settings = monetizationSettings;
            if (initialized || initializationRequested)
            {
                return;
            }

            if (settings == null)
            {
                Debug.LogError("[UnityAdsService] MonetizationSettings not assigned.");
                return;
            }

            var gameId = settings.GetUnityAdsGameId();
            if (string.IsNullOrWhiteSpace(gameId))
            {
                Debug.LogError("[UnityAdsService] Unity Ads Game ID is empty.");
                return;
            }

            rewardedPlacement = settings.rewardedPlacementId;
            interstitialPlacement = settings.interstitialPlacementId;
            bannerPlacement = settings.bannerPlacementId;

#if UNITY_ADS
            initializationRequested = true;
            Advertisement.Initialize(gameId, settings.unityAdsTestMode, this);
#else
            Debug.LogWarning("[UnityAdsService] UNITY_ADS define missing or Unity Ads package not installed.");
#endif
        }

        public bool IsRewardedReady(string placementId)
        {
#if UNITY_ADS
            return rewardedLoaded && Advertisement.IsReady(ResolveRewardedId(placementId));
#else
            return false;
#endif
        }

        public bool IsInterstitialReady(string placementId)
        {
#if UNITY_ADS
            return interstitialLoaded && Advertisement.IsReady(ResolveInterstitialId(placementId));
#else
            return false;
#endif
        }

        public void ShowRewarded(string placementId, Action<AdsShowResult> callback)
        {
#if UNITY_ADS
            if (!EnsureInitialized(callback))
            {
                return;
            }

            if (!rewardedLoaded)
            {
                callback?.Invoke(AdsShowResult.Failed);
                LoadRewarded();
                return;
            }

            rewardedCallback = callback;
            Advertisement.Show(ResolveRewardedId(placementId), this);
#else
            callback?.Invoke(AdsShowResult.Failed);
#endif
        }

        public void ShowInterstitial(string placementId, Action<AdsShowResult> callback)
        {
#if UNITY_ADS
            if (!EnsureInitialized(callback))
            {
                return;
            }

            if (!interstitialLoaded)
            {
                callback?.Invoke(AdsShowResult.Failed);
                LoadInterstitial();
                return;
            }

            interstitialCallback = callback;
            Advertisement.Show(ResolveInterstitialId(placementId), this);
#else
            callback?.Invoke(AdsShowResult.Failed);
#endif
        }

        public void ShowBanner(string placementId)
        {
#if UNITY_ADS
            if (!EnsureInitialized())
            {
                return;
            }

            bannerShowRequested = true;
            if (bannerLoaded)
            {
                Advertisement.Banner.Show(ResolveBannerId(placementId));
                return;
            }

            LoadBanner();
#endif
        }

        public void HideBanner(string placementId)
        {
#if UNITY_ADS
            bannerShowRequested = false;
            Advertisement.Banner.Hide();
#endif
        }

#if UNITY_ADS
        public void OnInitializationComplete()
        {
            initialized = true;
            initializationRequested = false;
            LoadRewarded();
            LoadInterstitial();
            LoadBanner();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            initialized = false;
            initializationRequested = false;
            Debug.LogWarning($"[UnityAdsService] Init failed: {error} {message}");
        }

        public void OnUnityAdsAdLoaded(string placementId)
        {
            if (IsPlacement(placementId, rewardedPlacement))
            {
                rewardedLoaded = true;
                return;
            }

            if (IsPlacement(placementId, interstitialPlacement))
            {
                interstitialLoaded = true;
            }
        }

        public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
        {
            if (IsPlacement(placementId, rewardedPlacement))
            {
                rewardedLoaded = false;
            }

            if (IsPlacement(placementId, interstitialPlacement))
            {
                interstitialLoaded = false;
            }

            Debug.LogWarning($"[UnityAdsService] Load failed ({placementId}): {error} {message}");
        }

        public void OnUnityAdsShowStart(string placementId)
        {
        }

        public void OnUnityAdsShowClick(string placementId)
        {
        }

        public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
        {
            var result = AdsShowResult.Failed;
            DispatchShowResult(placementId, result);
            Debug.LogWarning($"[UnityAdsService] Show failed ({placementId}): {error} {message}");
        }

        public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
        {
            var result = showCompletionState == UnityAdsShowCompletionState.COMPLETED
                ? AdsShowResult.Completed
                : AdsShowResult.Skipped;

            DispatchShowResult(placementId, result);
        }

        private void LoadRewarded()
        {
            if (!string.IsNullOrEmpty(rewardedPlacement))
            {
                Advertisement.Load(rewardedPlacement, this);
            }
        }

        private void LoadInterstitial()
        {
            if (!string.IsNullOrEmpty(interstitialPlacement))
            {
                Advertisement.Load(interstitialPlacement, this);
            }
        }

        private void LoadBanner()
        {
            if (string.IsNullOrEmpty(bannerPlacement))
            {
                return;
            }

            Advertisement.Banner.SetPosition(BannerPosition.BOTTOM_CENTER);
            Advertisement.Banner.Load(bannerPlacement, new BannerLoadOptions
            {
                loadCallback = () =>
                {
                    bannerLoaded = true;
                    if (bannerShowRequested)
                    {
                        Advertisement.Banner.Show(bannerPlacement);
                    }
                },
                errorCallback = _ => { bannerLoaded = false; }
            });
        }

        private void DispatchShowResult(string placementId, AdsShowResult result)
        {
            if (IsPlacement(placementId, rewardedPlacement))
            {
                rewardedCallback?.Invoke(result);
                rewardedCallback = null;
                rewardedLoaded = false;
                LoadRewarded();
                return;
            }

            if (IsPlacement(placementId, interstitialPlacement))
            {
                interstitialCallback?.Invoke(result);
                interstitialCallback = null;
                interstitialLoaded = false;
                LoadInterstitial();
            }
        }

        private bool EnsureInitialized(Action<AdsShowResult> callback = null)
        {
            if (initialized)
            {
                return true;
            }

            callback?.Invoke(AdsShowResult.Failed);
            return false;
        }
#endif

        private string ResolveRewardedId(string placementId)
        {
            if (!string.IsNullOrEmpty(placementId))
            {
                return placementId;
            }

            return rewardedPlacement;
        }

        private string ResolveInterstitialId(string placementId)
        {
            if (!string.IsNullOrEmpty(placementId))
            {
                return placementId;
            }

            return interstitialPlacement;
        }

        private string ResolveBannerId(string placementId)
        {
            if (!string.IsNullOrEmpty(placementId))
            {
                return placementId;
            }

            return bannerPlacement;
        }

        private static bool IsPlacement(string placementId, string expected)
        {
            return !string.IsNullOrEmpty(expected)
                && string.Equals(placementId, expected, StringComparison.Ordinal);
        }
    }
}
