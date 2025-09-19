using System.Linq;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Events;

namespace App.UseCase.Reward
{
    public class ClaimGoldUseCase : IClaimGoldUseCase
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IEventBus _eventBus;

        public ClaimGoldUseCase(IInventoryRepository inventoryRepository, IEventBus eventBus)
        {
            _inventoryRepository = inventoryRepository;
            _eventBus = eventBus;
        }

        public async UniTask Claim(int quantity)
        {
            if (quantity <= 0) return;

            // Traer inventario actual para conocer el currencyId de oro
            var inventory = await _inventoryRepository.GetPlayerInventory();
            var currency =
                inventory.Currencies.FirstOrDefault(c => c.Id == "coins"); // 👈 depende de tu setup en Unity Cloud
            if (currency == null)
                throw new System.Exception("Currency 'coins' not found in inventory.");

            // Incrementar balance
            await _inventoryRepository.IncrementCurrency(currency.Id, quantity);

            // Obtener nuevo estado
            var updatedInventory = await _inventoryRepository.GetPlayerInventory();
            var updatedCurrency = updatedInventory.Currencies.First(c => c.Id == "coins");

            // Publicar evento para UI
            _eventBus.Publish(new CurrencyChangedEvent(currency.Id, updatedCurrency.Amount));
        }
    }
}