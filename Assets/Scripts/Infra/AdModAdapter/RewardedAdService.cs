using GoogleMobileAds.Api;
using UnityEngine;

namespace Infra.AdModAdapter
{
    public class RewardedAdService : IAdService
    {
        private RewardedAd _currentAd;
        private readonly string _adUnitId;
        private bool _isInitialized;
        private bool _isShowing;

        public RewardedAdService(string adUnitId)
        {
            _adUnitId = adUnitId;
        }

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            LoadAd();
        }

        public void LoadAd()
        {
            if (!_isInitialized) return;

            var request = new AdRequest();
            RewardedAd.Load(_adUnitId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"Rewarded failed to load: {error}");
                    return;
                }

                _currentAd?.Destroy();
                _currentAd = ad;

                _currentAd.OnAdFullScreenContentClosed += () =>
                {
                    _isShowing = false;
                    LoadAd();
                };
            });
        }

        public void ShowAd(System.Action<double> onRewardEarned = null)
        {
            if (_currentAd != null && _currentAd.CanShowAd())
            {
                _currentAd.Show(reward =>
                {
                    Debug.Log($"Reward earned: {reward.Type}, amount: {reward.Amount}");
                    onRewardEarned?.Invoke(reward.Amount);
                });
                _isShowing = true;
            }
            else
            {
                LoadAd();
            }
        }

        public bool AdFinished() => !_isShowing;
    }
}