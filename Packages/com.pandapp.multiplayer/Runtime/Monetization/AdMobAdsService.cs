using System;
using UnityEngine;
#if PANDAPP_ADMOB
using GoogleMobileAds.Api;
#endif

namespace Pandapp.Multiplayer.Monetization
{
    public sealed class AdMobAdsService : MonoBehaviour, IAdsService
    {
        private const string NotConfiguredMessage = "[AdMobAdsService] AdMob adapter not configured.";

        private MonetizationSettings settings;
        private bool initialized;
        private bool initializationRequested;
        private bool bannerShowRequested;
        private string rewardedPlacement;
        private string interstitialPlacement;
        private string bannerPlacement;

#if PANDAPP_ADMOB
        private RewardedAd rewardedAd;
        private InterstitialAd interstitialAd;
        private BannerView bannerView;
        private bool rewardGranted;
        private Action<AdsShowResult> rewardedCallback;
        private Action<AdsShowResult> interstitialCallback;
#endif

        public AdsProvider Provider => AdsProvider.AdMob;
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
                Debug.LogWarning($"{NotConfiguredMessage} Missing settings.");
                return;
            }

            var appId = settings.GetAdMobAppId();
            if (string.IsNullOrWhiteSpace(appId))
            {
                Debug.LogWarning($"{NotConfiguredMessage} AdMob App ID missing.");
                return;
            }

            rewardedPlacement = settings.rewardedPlacementId;
            interstitialPlacement = settings.interstitialPlacementId;
            bannerPlacement = settings.bannerPlacementId;

#if PANDAPP_ADMOB
            initializationRequested = true;
            MobileAds.Initialize(_ =>
            {
                initializationRequested = false;
                initialized = true;
                LoadRewarded();
                LoadInterstitial();
                if (bannerShowRequested)
                {
                    LoadBanner();
                }
            });
#else
            Debug.LogWarning($"{NotConfiguredMessage} Define PANDAPP_ADMOB and install Google Mobile Ads SDK.");
#endif
        }

        public bool IsRewardedReady(string placementId)
        {
#if PANDAPP_ADMOB
            return rewardedAd != null && rewardedAd.CanShowAd();
#else
            return false;
#endif
        }

        public bool IsInterstitialReady(string placementId)
        {
#if PANDAPP_ADMOB
            return interstitialAd != null && interstitialAd.CanShowAd();
#else
            return false;
#endif
        }

        public void ShowRewarded(string placementId, Action<AdsShowResult> callback)
        {
#if PANDAPP_ADMOB
            if (!EnsureInitialized(callback))
            {
                return;
            }

            if (!IsRewardedReady(placementId))
            {
                callback?.Invoke(AdsShowResult.Failed);
                LoadRewarded();
                return;
            }

            rewardGranted = false;
            rewardedCallback = callback;
            rewardedAd.Show(_ => { rewardGranted = true; });
#else
            callback?.Invoke(AdsShowResult.Failed);
#endif
        }

        public void ShowInterstitial(string placementId, Action<AdsShowResult> callback)
        {
#if PANDAPP_ADMOB
            if (!EnsureInitialized(callback))
            {
                return;
            }

            if (!IsInterstitialReady(placementId))
            {
                callback?.Invoke(AdsShowResult.Failed);
                LoadInterstitial();
                return;
            }

            interstitialCallback = callback;
            interstitialAd.Show();
#else
            callback?.Invoke(AdsShowResult.Failed);
#endif
        }

        public void ShowBanner(string placementId)
        {
#if PANDAPP_ADMOB
            if (!EnsureInitialized())
            {
                return;
            }

            bannerShowRequested = true;
            if (bannerView != null)
            {
                bannerView.Show();
                return;
            }

            LoadBanner();
#endif
        }

        public void HideBanner(string placementId)
        {
#if PANDAPP_ADMOB
            bannerShowRequested = false;
            bannerView?.Hide();
#endif
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

#if PANDAPP_ADMOB
        private void LoadRewarded()
        {
            var placementId = ResolveRewardedId();
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return;
            }

            DestroyRewarded();

            var request = CreateAdRequest();
            RewardedAd.Load(placementId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    return;
                }

                rewardedAd = ad;
                BindRewardedCallbacks();
            });
        }

        private void LoadInterstitial()
        {
            var placementId = ResolveInterstitialId();
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return;
            }

            DestroyInterstitial();

            var request = CreateAdRequest();
            InterstitialAd.Load(placementId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    return;
                }

                interstitialAd = ad;
                BindInterstitialCallbacks();
            });
        }

        private void LoadBanner()
        {
            var placementId = ResolveBannerId();
            if (string.IsNullOrWhiteSpace(placementId))
            {
                return;
            }

            bannerView?.Destroy();
            bannerView = new BannerView(placementId, AdSize.Banner, AdPosition.Bottom);
            var request = CreateAdRequest();
            bannerView.LoadAd(request);
            if (bannerShowRequested)
            {
                bannerView.Show();
            }
        }

        private static AdRequest CreateAdRequest()
        {
            var builderType = typeof(AdRequest).GetNestedType("Builder");
            if (builderType != null)
            {
                var builder = Activator.CreateInstance(builderType);
                var buildMethod = builderType.GetMethod("Build");
                if (buildMethod != null)
                {
                    return buildMethod.Invoke(builder, null) as AdRequest;
                }
            }

            return new AdRequest();
        }

        private void BindRewardedCallbacks()
        {
            if (rewardedAd == null)
            {
                return;
            }

            rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                var result = rewardGranted ? AdsShowResult.Completed : AdsShowResult.Skipped;
                rewardedCallback?.Invoke(result);
                rewardedCallback = null;
                LoadRewarded();
            };

            rewardedAd.OnAdFullScreenContentFailed += _ =>
            {
                rewardedCallback?.Invoke(AdsShowResult.Failed);
                rewardedCallback = null;
                LoadRewarded();
            };
        }

        private void BindInterstitialCallbacks()
        {
            if (interstitialAd == null)
            {
                return;
            }

            interstitialAd.OnAdFullScreenContentClosed += () =>
            {
                interstitialCallback?.Invoke(AdsShowResult.Completed);
                interstitialCallback = null;
                LoadInterstitial();
            };

            interstitialAd.OnAdFullScreenContentFailed += _ =>
            {
                interstitialCallback?.Invoke(AdsShowResult.Failed);
                interstitialCallback = null;
                LoadInterstitial();
            };
        }

        private void DestroyRewarded()
        {
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }
        }

        private void DestroyInterstitial()
        {
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }
        }

        private string ResolveRewardedId()
        {
            return rewardedPlacement;
        }

        private string ResolveInterstitialId()
        {
            return interstitialPlacement;
        }

        private string ResolveBannerId()
        {
            return bannerPlacement;
        }

        private void OnDestroy()
        {
            DestroyRewarded();
            DestroyInterstitial();
            bannerView?.Destroy();
        }
#else
        private string ResolveRewardedId()
        {
            return rewardedPlacement;
        }

        private string ResolveInterstitialId()
        {
            return interstitialPlacement;
        }

        private string ResolveBannerId()
        {
            return bannerPlacement;
        }
#endif
    }
}
