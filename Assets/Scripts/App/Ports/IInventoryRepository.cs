using Cysharp.Threading.Tasks;
using Domain.Model;

namespace App.Ports
{
    public interface IInventoryRepository
    {
        UniTask<PlayerInventory> GetPlayerInventory();
        UniTask AddPowerUp(string powerUpId, int quantity);
        UniTask ConsumePowerUp(string powerUpId, int quantity);
        UniTask DecrementCurrency(string currencyId, int amount);
    }
}