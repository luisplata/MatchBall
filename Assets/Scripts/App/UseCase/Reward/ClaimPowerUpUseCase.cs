using App.Ports;
using Cysharp.Threading.Tasks;

namespace App.UseCase.Reward
{
    public class ClaimPowerUpUseCase : IClaimPowerUpUseCase
    {
        private readonly IInventoryRepository _inventoryRepository;

        public ClaimPowerUpUseCase(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository;
        }

        public async UniTask Claim(PowerUpData powerUpSelected)
        {
            await _inventoryRepository.AddPowerUp(powerUpSelected.id, powerUpSelected.quantity);
        }
    }
}