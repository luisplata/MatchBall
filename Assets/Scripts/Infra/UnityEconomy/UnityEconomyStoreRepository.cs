using System;
using System.Collections.Generic;
using System.Linq;
using App.Ports;
using Cysharp.Threading.Tasks;
using Domain.Model;
using Presentation.Game;
using Unity.Services.Economy;
using UnityEngine;
using PlayerInventory = Domain.Model.PlayerInventory;

namespace Infrastructure.UnityEconomy
{
    // Clase auxiliar para deserializar instanceData.count
    internal class InventoryInstanceData
    {
        public int count;
    }

    public class UnityEconomyStoreRepository : IStoreRepository, IInventoryRepository
    {
        public async UniTask<List<PurchaseOption>> GetAvailablePurchases()
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            var purchases = EconomyService.Instance.Configuration.GetVirtualPurchases();

            var result = new List<PurchaseOption>();
            foreach (var p in purchases)
            {
                var cost = p.Costs.FirstOrDefault();
                var currencyId = cost?.Item?.GetReferencedConfigurationItem()?.Id;
                var price = cost?.Amount ?? 0;

                var reward = p.Rewards.FirstOrDefault();
                var rewardDef = reward?.Item?.GetReferencedConfigurationItem();

                var purchaseCustom = p.CustomDataDeserializable.GetAs<PurchaseCustomData>();
                var rewardCustom = rewardDef?.CustomDataDeserializable?.GetAs<RewardCustomData>();

                var option = new PurchaseOption(
                    p.Id,
                    purchaseCustom?.itemName ?? p.Name,
                    purchaseCustom?.itemImage ?? "",
                    new List<(string, int)> { (currencyId, price) },
                    (rewardDef?.Id, rewardCustom?.count, rewardCustom?.type,
                        ServiceLocator.Instance.GetService<IGlobalSessionService>()
                            .howMuchPowerUpsByBuy(rewardCustom?.count))
                );

                result.Add(option);
            }

            return result;
        }

        public async UniTask<PlayerInventory> GetPlayerInventory()
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            // 1) Traer balances (monedas)
            var balancesResult = await EconomyService.Instance.PlayerBalances.GetBalancesAsync();
            var currencies = new List<Currency>();
            foreach (var b in balancesResult.Balances)
            {
                // PlayerBalance.Balance es long; cast seguro a int si tu dominio usa int
                currencies.Add(new Currency(b.CurrencyId, (int)b.Balance));
            }

            // 2) Traer items del inventario (instancias)
            var inventoryResult = await EconomyService.Instance.PlayerInventory.GetInventoryAsync();
            var powerUps = new List<PowerUp>();

            foreach (var pItem in inventoryResult.PlayersInventoryItems)
            {
                // Obtener definición de configuración del inventory item
                // (PlayersInventoryItem proporciona GetItemDefinition()).
                var def = pItem.GetItemDefinition(); // InventoryItemDefinition
                // Leer instanceData.count si existe (stacking)
                var instData = pItem.InstanceData != null ? pItem.InstanceData.GetAs<InventoryInstanceData>() : null;
                int count = instData?.count ?? 1;

                powerUps.Add(new PowerUp(
                    def.Id,
                    def.Name,
                    def.Type,
                    count
                ));
            }

            return new PlayerInventory(powerUps, currencies);
        }

        public async UniTask DecrementCurrency(string currencyId, int amount)
        {
            await EconomyService.Instance.PlayerBalances.DecrementBalanceAsync(currencyId, amount);
        }

        public async UniTask<List<PowerUpData>> GetPowerUpsInventoryFromStore()
        {
            await EconomyService.Instance.Configuration.SyncConfigurationAsync();
            var purchases = EconomyService.Instance.Configuration.GetVirtualPurchases();

            var result = new List<PowerUpData>();

            foreach (var p in purchases)
            {
                var reward = p.Rewards.FirstOrDefault();
                var rewardDef = reward?.Item?.GetReferencedConfigurationItem();

                if (rewardDef == null)
                    continue;

                var rewardCustom = rewardDef.CustomDataDeserializable?.GetAs<RewardCustomData>();
                if (rewardCustom == null)
                    continue;

                // solo mapear si es de tipo "powerup"
                if (rewardCustom.type == "powerup")
                {
                    result.Add(new PowerUpData
                    {
                        id = rewardDef.Id,
                        selected = false
                    });
                }
            }

            return result;
        }
        
        [System.Serializable]
        public class RewardCustomData
        {
            public string count;
            public string type;
        }

        public async UniTask IncrementCurrency(string currencyId, int amount)
        {
            await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync(currencyId, amount);
        }


        public async UniTask AddPowerUp(string powerUpId, int quantity)
        {
            try
            {
                if (quantity <= 0) return;

                // Buscar instancias existentes del mismo inventoryItemId
                var opts = new GetInventoryOptions
                    { InventoryItemIds = new List<string> { powerUpId }, ItemsPerFetch = 20 };
                var inv = await EconomyService.Instance.PlayerInventory.GetInventoryAsync(opts);

                var existing = inv.PlayersInventoryItems.FirstOrDefault();

                if (existing != null)
                {
                    // Si ya existe una instancia con instanceData.count la incrementamos
                    var inst = existing.InstanceData != null
                        ? existing.InstanceData.GetAs<InventoryInstanceData>()
                        : null;
                    int current = inst?.count ?? 1; // si no hay instanceData asumimos 1
                    int newCount = current + quantity;

                    var instanceDict = new Dictionary<string, object> { { "count", newCount } };
                    var updateOptions = new UpdatePlayersInventoryItemOptions
                    {
                        WriteLock = existing.WriteLock // usar writeLock para concurrencia segura
                    };

                    await EconomyService.Instance.PlayerInventory.UpdatePlayersInventoryItemAsync(
                        existing.PlayersInventoryItemId,
                        instanceDict,
                        updateOptions
                    );
                }
                else
                {
                    // No existe: crear una nueva instancia con instanceData.count = quantity
                    var addOptions = new AddInventoryItemOptions
                    {
                        InstanceData = new Dictionary<string, object> { { "count", quantity } }
                    };

                    await EconomyService.Instance.PlayerInventory.AddInventoryItemAsync(powerUpId, addOptions);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public async UniTask ConsumePowerUp(string powerUpId, int quantity)
        {
            if (quantity <= 0) return;

            var opts = new GetInventoryOptions
                { InventoryItemIds = new List<string> { powerUpId }, ItemsPerFetch = 50 };
            var inv = await EconomyService.Instance.PlayerInventory.GetInventoryAsync(opts);

            var items = inv.PlayersInventoryItems.ToList();
            if (!items.Any())
                throw new Exception($"No hay '{powerUpId}' en el inventario.");

            int remaining = quantity;

            // iteramos instancias y consumimos (actualizamos count o borramos la instancia)
            foreach (var item in items)
            {
                if (remaining <= 0) break;

                var inst = item.InstanceData != null ? item.InstanceData.GetAs<InventoryInstanceData>() : null;
                int current = inst?.count ?? 1;

                if (current > remaining)
                {
                    // actualizamos la misma instancia con nuevo count
                    var newCount = current - remaining;
                    var instanceDict = new Dictionary<string, object> { { "count", newCount } };
                    var updateOptions = new UpdatePlayersInventoryItemOptions
                    {
                        WriteLock = item.WriteLock
                    };

                    await EconomyService.Instance.PlayerInventory.UpdatePlayersInventoryItemAsync(
                        item.PlayersInventoryItemId,
                        instanceDict,
                        updateOptions
                    );

                    remaining = 0;
                    break;
                }
                else
                {
                    // borramos la instancia completa (consume current)
                    var deleteOptions = new DeletePlayersInventoryItemOptions
                    {
                        WriteLock = item.WriteLock
                    };

                    await EconomyService.Instance.PlayerInventory.DeletePlayersInventoryItemAsync(
                        item.PlayersInventoryItemId,
                        deleteOptions
                    );

                    remaining -= current;
                }
            }

            if (remaining > 0)
                throw new Exception(
                    $"No tenía suficientes '{powerUpId}' para consumir {quantity}. Faltaron {remaining}.");
        }
    }
}