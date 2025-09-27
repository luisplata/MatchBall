using Cysharp.Threading.Tasks;

namespace App.UseCase.Reward
{
    public interface IClaimPowerUpUseCase
    {
        UniTask Claim(PowerUpData powerUpSelected);
    }
}