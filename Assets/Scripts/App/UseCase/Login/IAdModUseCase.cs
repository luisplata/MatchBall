using System;

namespace App.UseCase
{
    public interface IAdModUseCase
    {
        void ShowInterstitial(System.Action<double> onClosed = null);
        bool IsInterstitialFinished();

        // Rewarded
        void ShowRewarded(System.Action<double> onRewardEarned = null);
        bool IsRewardedFinished();
    }
}