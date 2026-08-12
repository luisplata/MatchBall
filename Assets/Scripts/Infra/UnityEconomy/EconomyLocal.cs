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
    public class EconomyLocal : IStoreRepository, IInventoryRepository
    {
        public async UniTask<List<PurchaseOption>> GetAvailablePurchases()
        {
            try
            {
                var list = new List<PurchaseOption>();

                list.Add(new PurchaseOption(
                    "BUY_ICE",
                    "buy_ice",
                    "ICE",
                    new List<(string, int)> { ("ICE", 100) },
                    ("POWERUP_ICE", "POWERUP_ICE", "POWERUP_ICE",
                        ServiceLocator.Instance.GetService<IGlobalSessionService>()
                            .HowMuchPowerUpsByBuy("POWERUP_ICE"))
                ));
                list.Add(new PurchaseOption(
                    "BUY_JOKER",
                    "buy_joker",
                    "JOKER",
                    new List<(string, int)> { ("JOKER", 100) },
                    ("POWERUP_JOKER", "POWERUP_JOKER", "POWERUP_JOKER",
                        ServiceLocator.Instance.GetService<IGlobalSessionService>()
                            .HowMuchPowerUpsByBuy("POWERUP_JOKER"))
                ));
                list.Add(new PurchaseOption(
                    "BUY_LEAD",
                    "buy_lead",
                    "LEAD",
                    new List<(string, int)> { ("LEAD", 100) },
                    ("POWERUP_LEAD", "POWERUP_LEAD", "POWERUP_LEAD",
                        ServiceLocator.Instance.GetService<IGlobalSessionService>()
                            .HowMuchPowerUpsByBuy("POWERUP_LEAD"))
                ));
                list.Add(new PurchaseOption(
                    "BUY_RAY",
                    "buy_ray",
                    "RAY",
                    new List<(string, int)> { ("RAY", 100) },
                    ("POWERUP_RAY", "POWERUP_RAY", "POWERUP_RAY",
                        ServiceLocator.Instance.GetService<IGlobalSessionService>()
                            .HowMuchPowerUpsByBuy("POWERUP_RAY"))
                ));

                return list;
            }
            catch (Exception e)
            {
                // Error global, no crashea el juego
                Debug.LogError($"[Economy] Error al obtener compras: {e}");
                ServiceLocator.Instance.GetService<IMessageService>()?.ErrorMessage(e, 10).Forget();
                return new List<PurchaseOption>();
            }
        }

        private PlayerInventory GetInventory()
        {
            string jsonData = PlayerPrefs.GetString("PlayerInventory", "{\"PowerUps\":[],\"Currencies\":[]}");
            PlayerInventory inventory = JsonUtility.FromJson<PlayerInventory>(jsonData);
            return inventory;
        }

        private void SaveInventory(PlayerInventory inventory)
        {
            string jsonString = JsonUtility.ToJson(inventory);
            PlayerPrefs.SetString("PlayerInventory", jsonString);
            PlayerPrefs.Save();
        }

        public async UniTask<PlayerInventory> GetPlayerInventory()
        {
            var currencies = new List<Currency> { new("GOLD", PlayerPrefs.GetInt("GOLD", 0)) };

            var powerUps = GetInventory().PowerUps
                .Select(pItem => new PowerUp(pItem.Id, pItem.Name, pItem.Type, pItem.Quantity)).ToList();

            return new PlayerInventory(powerUps, currencies);
        }

        public async UniTask DecrementCurrency(string currencyId, int amount)
        {
            int currentAmount = PlayerPrefs.GetInt(currencyId, 0);
            int newAmount = Mathf.Max(0, currentAmount - amount);
            PlayerPrefs.SetInt(currencyId, newAmount);
        }

        public async UniTask<List<PowerUpData>> GetPowerUpsInventoryFromStore()
        {
            var result = new List<PowerUpData>();

            for (int i = 0; i < GetInventory().PowerUps.Count; i++)
            {
                var pItem = GetInventory().PowerUps[i];
                result.Add(new PowerUpData()
                {
                    id = pItem.Id, selected = false
                });
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
            int currentAmount = PlayerPrefs.GetInt(currencyId, 0);
            int newAmount = currentAmount + amount;
            PlayerPrefs.SetInt(currencyId, newAmount);
            PlayerPrefs.Save();
        }


        public async UniTask AddPowerUp(string powerUpId, int quantity)
        {
            try
            {
                var inventory = GetInventory();
                var existingPowerUp = inventory.PowerUps.FirstOrDefault(p => p.Id == powerUpId);

                if (existingPowerUp != null)
                {
                    existingPowerUp.Add(quantity);
                }
                else
                {
                    inventory.PowerUps.Add(new PowerUp(powerUpId, powerUpId, powerUpId, quantity));
                }

                SaveInventory(inventory);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                throw;
            }
        }

        public async UniTask ConsumePowerUp(string powerUpId, int quantity)
        {
            var inventory = GetInventory();
            var existingPowerUp = inventory.PowerUps.FirstOrDefault(p => p.Id == powerUpId);

            if (existingPowerUp == null)
                throw new Exception($"No se encontró el power-up '{powerUpId}' en el inventario.");

            existingPowerUp.Consume(quantity);
            SaveInventory(inventory);
        }
    }
}