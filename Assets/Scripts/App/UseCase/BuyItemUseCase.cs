using System;
using System.Linq;
using App.ExceptionsToGame;
using App.Ports;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace App.UseCase
{
    public class BuyItemUseCase : IBuyItemUseCase
    {
        private readonly IStoreRepository _storeRepository;
        private readonly IInventoryRepository _inventoryRepository;

        public BuyItemUseCase(IStoreRepository storeRepository, IInventoryRepository inventoryRepository)
        {
            _storeRepository = storeRepository;
            _inventoryRepository = inventoryRepository;
        }

        public async UniTask Execute(string purchaseId)
        {
            var purchases = await _storeRepository.GetAvailablePurchases();
            var purchase = purchases.FirstOrDefault(p => p.PurchaseId == purchaseId);
            if (purchase == null)
                throw new BuyException("Purchase not found");
            var playerRepository = await _inventoryRepository.GetPlayerInventory();
            var coins = playerRepository.Currencies.First().Amount;
            if (coins < purchase.Costs.First().amount) throw new BuyException("Not enough coins!");

            await _inventoryRepository.AddPowerUp(purchase.Reward.identify, purchase.Reward.quantity);
        }
    }
}