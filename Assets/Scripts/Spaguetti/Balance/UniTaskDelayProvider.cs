using Cysharp.Threading.Tasks;

namespace Spaguetti.Balance
{
    public class UniTaskDelayProvider : IDelayProvider
    {
        public UniTask DelaySeconds(float seconds)
        {
            return UniTask.Delay(System.TimeSpan.FromSeconds(seconds));
        }
    }
}
