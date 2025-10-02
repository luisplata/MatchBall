using App.Ports;
using UnityEngine;

namespace Infra.AdModAdapter
{
    public class FakeAdModAdapter : IAdModAdapter
    {
        private bool _interstitialFinished;
        private bool _rewardedFinished;

        public void Initialize()
        {
            Debug.Log("[FakeAds] Inicialización simulada completa.");
        }

        public void ShowAd()
        {
            Debug.Log("[FakeAds] ShowAd() llamado (sin tipo específico).");
            // Simulamos que siempre finaliza correctamente
            _interstitialFinished = true;
            _rewardedFinished = true;
        }

        public bool AdFinished()
        {
            Debug.Log("[FakeAds] AdFinished() llamado.");
            return true;
        }

        // Métodos extra para simular los 2 tipos de anuncios
        public void ShowInterstitial(System.Action<double> onClosed = null)
        {
            Debug.Log("[FakeAds] Mostrando Interstitial (simulado).");
            _interstitialFinished = true;

            // Simulamos cierre con callback
            onClosed?.Invoke(0);
        }

        public void ShowRewarded(System.Action<double> onRewardEarned = null)
        {
            Debug.Log("[FakeAds] Mostrando Rewarded (simulado).");
            _rewardedFinished = true;

            // Simulamos que el usuario siempre gana la recompensa
            onRewardEarned?.Invoke(1);
        }

        public bool IsInterstitialFinished() => _interstitialFinished;

        public bool IsRewardedFinished() => _rewardedFinished;
    }
}