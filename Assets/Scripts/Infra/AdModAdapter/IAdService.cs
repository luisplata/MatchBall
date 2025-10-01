namespace Infra.AdModAdapter
{
    public interface IAdService
    {
        void Initialize();
        void ShowAd(System.Action<double> onCompleted = null);
        bool AdFinished();
    }
}