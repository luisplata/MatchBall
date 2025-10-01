namespace App.Ports
{
    public interface IAdModAdapter
    {
        void Initialize();

        // Interstitial
        void ShowInterstitial(System.Action<double> onClosed = null);
        bool IsInterstitialFinished();

        // Rewarded
        void ShowRewarded(System.Action<double> onRewardEarned = null);
        bool IsRewardedFinished();
    }
}