using Cysharp.Threading.Tasks;

namespace App.UseCase.Reward
{
    public interface IClaimGoldUseCase
    {
        UniTask Claim(int quantity);
    }
}