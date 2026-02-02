using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance
{
    public interface IDelayProvider
    {
        UniTask DelaySeconds(float seconds);
    }
}
