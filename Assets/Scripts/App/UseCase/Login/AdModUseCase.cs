using System;
using App.Ports;

namespace App.UseCase
{
    public class AdModUseCase : IAdModUseCase
    {
        private readonly IAdModAdapter _adModAdapter;

        public AdModUseCase(IAdModAdapter adModAdapter)
        {
            _adModAdapter = adModAdapter;
        }

        public void ShowInterstitial(Action<double> onClosed = null)
        {
            _adModAdapter.ShowInterstitial(onClosed);
        }

        public bool IsInterstitialFinished()
        {
            return _adModAdapter.IsInterstitialFinished();
        }

        public void ShowRewarded(Action<double> onRewardEarned = null)
        {
            _adModAdapter.ShowRewarded(onRewardEarned);
        }

        public bool IsRewardedFinished()
        {
            return _adModAdapter.IsRewardedFinished();
        }
    }
}