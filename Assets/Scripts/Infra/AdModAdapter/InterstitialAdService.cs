using GoogleMobileAds.Api;
using UnityEngine;

namespace Infra.AdModAdapter
{
    public class InterstitialAdService : IAdService
    {
        private InterstitialAd _currentAd;
        private readonly string _adUnitId;
        private bool _isInitialized;
        private bool _isShowing;

        public InterstitialAdService(string adUnitId)
        {
            _adUnitId = adUnitId;
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            MobileAds.Initialize(_ =>
            {
                Debug.Log("AdMob Inter initialized.");
                _isInitialized = true;
                LoadAd();
            });
        }

        public void LoadAd()
        {
            if (!_isInitialized) return;

            var request = new AdRequest();
            InterstitialAd.Load(_adUnitId, request, (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogError($"Interstitial failed to load: {error}");
                    return;
                }

                _currentAd?.Destroy();
                _currentAd = ad;

                _currentAd.OnAdFullScreenContentClosed += () =>
                {
                    _isShowing = false;
                    LoadAd(); // precargar el siguiente
                };
            });
        }

        public void ShowAd(System.Action<double> onCompleted = null)
        {
            if (_currentAd != null && _currentAd.CanShowAd())
            {
                _currentAd.Show();
                _isShowing = true;

                _currentAd.OnAdFullScreenContentClosed += () =>
                {
                    _isShowing = false;
                    onCompleted?.Invoke(1.0);
                };
            }
            else
            {
                LoadAd();
            }
        }

        public bool AdFinished() => !_isShowing;
    }
}