using System;
using System.Collections.Generic;
using System.Linq;
using App.UseCase.PowerUps;
using App.UseCase.Reward;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Newtonsoft.Json;
using Presentation.Game;
using Unity.Services.CloudSave.Models;
using Unity.Services.Economy;
using UnityEngine;

[Serializable]
public class PlayerSessionDataCustom
{
    public string playerId;
    public string playerName;
    public string playerEmail;
    public string playerPhotoUrl;
    public bool isCreated;
    public int cups;
    public string language;
    public bool isMute;
    public float volume;
    public string powerups_json;

    public List<PowerUpData> GetPowerUpsDummy()
    {
#if UNITY_EDITOR
        // ListOfPowerUps
        return new List<PowerUpData>
        {
            new() { id = "POWERUP_LEAD", selected = false },
            new() { id = "POWERUP_RAY", selected = false },
            new() { id = "POWERUP_JOKER", selected = false },
            new() { id = "POWERUP_ICE", selected = false },
        };
#else
        // ListOfPowerUps
        return new List<PowerUpData>
        {
            new() { id = "POWERUP_LEAD", selected = false },
            new() { id = "POWERUP_RAY", selected = false },
            new() { id = "POWERUP_JOKER", selected = false },
            new() { id = "POWERUP_ICE", selected = false },
        };
#endif
    }

    public Dictionary<string, object> GetData()
    {
        return new Dictionary<string, object>
        {
            { "playerId", playerId },
            { "playerName", playerName },
            { "playerEmail", playerEmail },
            { "playerPhotoUrl", playerPhotoUrl },
            { "isCreated", isCreated },
            { "cups", cups },
            { "language", language },
            { "isMute", isMute },
            { "volume", volume },
            { "powerups_json", powerups_json }
        };
    }

    public async UniTask<int> GetCoins()
    {
        var options = new GetBalancesOptions
        {
            ItemsPerFetch = 5
        };

        var getBalancesResult = await EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
        var firstFiveBalances = getBalancesResult.Balances;
        // do something with your balances
        foreach (var balance in firstFiveBalances)
        {
            //Debug.Log($"CurrencyId: {balance.CurrencyId}");
            if (balance.CurrencyId == "COINS")
            {
                return (int)balance.Balance;
            }
        }

        throw new Exception("Coins not found");
    }

    public void LoadData(Dictionary<string, Item> loadAllAsync)
    {
        foreach (var result in loadAllAsync)
        {
            switch (result.Key)
            {
                case "playerId":
                    playerId = result.Value.Value.GetAsString();
                    break;
                case "playerName":
                    playerName = result.Value.Value.GetAsString();
                    break;
                case "playerEmail":
                    playerEmail = result.Value.Value.GetAsString();
                    break;
                case "playerPhotoUrl":
                    playerPhotoUrl = result.Value.Value.GetAsString();
                    break;
                case "isCreated":
                    isCreated = result.Value.Value.GetAs<bool>();
                    break;
                case "cups":
                    cups = result.Value.Value.GetAs<int>();
                    break;
                case "language":
                    language = result.Value.Value.GetAsString();
                    break;
                case "isMute":
                    isMute = result.Value.Value.GetAs<bool>();
                    break;
                case "volume":
                    volume = result.Value.Value.GetAs<float>();
                    break;
                case "powerups_json":
                    powerups_json = result.Value.Value.GetAsString();
                    break;
            }
        }
    }

    public List<PowerUpData> GetPowerUps()
    {
        try
        {
            Debug.Log(powerups_json);
            return JsonUtility.FromJson<PowerUpsData>(powerups_json).powerups;
        }
        catch (Exception e)
        {
            return new List<PowerUpData>();
        }
    }

    public async UniTask<RewardDataJson> AddReward(RewardData rewardData)
    {
        var rewardDataC = JsonConvert.DeserializeObject<RewardDataJson>(rewardData.Reward);


        Debug.Log(rewardDataC.type);

        switch (rewardDataC.type)
        {
            case "gold":
                var currency = JsonUtility.FromJson<CurrencyReward>(rewardDataC.body);
                await ServiceLocator.Instance.GetService<IPlayerSessionService>()
                    .AddCoins(currency.amount);
                break;
            case "powerup":
                //AddPowerUpToPlayer(rewardDataC.count);
                var powerup = JsonUtility.FromJson<PowerUpReward>(rewardDataC.body);
                var powerUpSelected = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetPowerUpsFromStore()
                    .First(p => p.id == powerup.powerup);
                powerUpSelected.quantity = powerup.quantity;
                await ServiceLocator.Instance.GetService<IClaimPowerUpUseCase>().Claim(powerUpSelected);
                break;
            default:
                throw new Exception($"Reward type {rewardDataC.type} not supported yet");
        }

        return rewardDataC;
    }

    [Obsolete("Use IClaimPowerUpUseCase or IBuyItemUseCase Services to claim or buy powerups")]
    public void AddPowerUpToPlayer(StoreItemDataFromCloud powerUp, int count)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = ServiceLocator.Instance.GetService<IGlobalSessionService>().GetListOfPowerUps();

        // Buscar definición del powerup en tu catálogo
        var powerUpData = powerUpsData.Find(x => x.id == powerUp.RewardItemId);
        if (powerUpData == null)
            throw new Exception($"El powerup {powerUp.RewardItemId} no existe en el catálogo global.");

        var allPowerUps = player.GetPowerUps();

        // Agregarlo count veces
        for (int i = 0; i < count; i++)
        {
            allPowerUps.Add(powerUpData);
        }

        // Guardar en JSON
        player.powerups_json = JsonUtility.ToJson(new PowerUpsData { powerups = allPowerUps });
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(player).Forget();
    }


    public bool CheckPowerUp(string powerUpId)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = ServiceLocator.Instance.GetService<IGlobalSessionService>().GetListOfPowerUps();
        var powerUpData = powerUpsData.Find(x => x.id == powerUpId);
        var allPowerUps = player.GetPowerUps();

        return allPowerUps.Any(x => x.id == powerUpData.id);
    }
}