using App.Ports;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Infra.AdModAdapter
{
    public class AdModAdapter : IAdModAdapter
    {
        private readonly IAdService _interstitialService;
        private readonly IAdService _rewardedService;

        public AdModAdapter(string interstitialId, string rewardedId)
        {
            _interstitialService = new InterstitialAdService(interstitialId);
            _rewardedService = new RewardedAdService(rewardedId);
        }

        public void Initialize()
        {
            MobileAds.Initialize((InitializationStatus initstatus) =>
            {
                if (initstatus == null)
                {
                    Debug.LogError("Google Mobile Ads initialization failed.");
                    return;
                }

                Debug.LogError("Google Mobile Ads initialization complete.");
                _interstitialService.Initialize();
                _rewardedService.Initialize();
            });
        }

        public void ShowInterstitial(System.Action<double> onClosed = null) => _interstitialService.ShowAd(onClosed);

        public void ShowRewarded(System.Action<double> onRewardEarned = null) =>
            _rewardedService.ShowAd(onRewardEarned);

        public bool IsInterstitialFinished() => _interstitialService.AdFinished();

        public bool IsRewardedFinished() => _rewardedService.AdFinished();
    }
}