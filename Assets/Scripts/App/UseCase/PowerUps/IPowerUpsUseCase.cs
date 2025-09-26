using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace App.UseCase.PowerUps
{
    public interface IPowerUpsUseCase
    {
        IReadOnlyList<PowerUpData> GetPowerUps();
        UniTask ConsumePowerUp(string id);
        UniTask LoadPowerUps();
        bool PowerUpSelected(string powerUpName, bool isSelected);
        IReadOnlyList<PowerUpData> GetSelectedPowerUps();
    }
}