using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
        // ListOfPowerUps
        return new List<PowerUpData>
        {
            new() { id = "lead", selected = false },
            new() { id = "ray", selected = false },
            new() { id = "joker", selected = false },
            new() { id = "ice", selected = false },
        };
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
        var rewardDataC = JsonUtility.FromJson<RewardDataJson>(rewardData.reward);

        Debug.Log(rewardDataC.type);
        Debug.Log(rewardDataC.count);

        switch (rewardDataC.type)
        {
            case "gold":
                await ServiceLocator.Instance.GetService<IPlayerSessionService>()
                    .AddCoins(int.Parse(rewardDataC.count));
                break;
            case "powerup":
                AddPowerUpToPlayer(rewardDataC.count);
                break;
            default:
                throw new Exception($"Reward type {rewardDataC.type} not supported yet");
        }

        return rewardDataC;
    }

    public void AddPowerUpToPlayer(string rewardDataC)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = ServiceLocator.Instance.GetService<IGlobalSessionService>().GetListOfPowerUps();
        var powerUpData = powerUpsData.Find(x => x.id == rewardDataC);
        var allPowerUps = player.GetPowerUps();

        if (allPowerUps.Any(x => x.id == rewardDataC))
        {
            throw new Exception($"El powerup {rewardDataC} ya esta en posesion del jugador");
        }

        allPowerUps.Add(powerUpData);
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