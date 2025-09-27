using System;
using System.Linq;
using App.ExceptionsToGame;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Events;
using UnityEngine;

namespace App.UseCase
{
    public class BuyItemUseCase : IBuyItemUseCase
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IEventBus _eventBus;

        public BuyItemUseCase(IStoreRepository storeRepository, IInventoryRepository inventoryRepository,
            IEventBus eventBus)
        {
            _storeRepository = storeRepository;
            _inventoryRepository = inventoryRepository;
            _eventBus = eventBus;
        }

        public async UniTask Execute(string purchaseId)
        {
            var purchases = await _storeRepository.GetAvailablePurchases();
            var purchase = purchases.FirstOrDefault(p => p.PurchaseId == purchaseId);
            if (purchase == null) throw new BuyException("Purchase not found");
            var playerRepository = await _inventoryRepository.GetPlayerInventory();
            var currency = playerRepository.Currencies.First();
            var coins = currency.Amount;
            var price = purchase.Costs.First().amount;
            if (coins < price) throw new BuyException("Not enough coins!");

            await _inventoryRepository.DecrementCurrency(currency.Id, price);

            await _inventoryRepository.AddPowerUp(purchase.Reward.identify, purchase.Reward.quantity);
            playerRepository = await _inventoryRepository.GetPlayerInventory();
            currency = playerRepository.Currencies.First();
            coins = currency.Amount;
            _eventBus.Publish(new CurrencyChangedEvent("coins", coins));
        }
    }
}