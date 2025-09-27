using Cysharp.Threading.Tasks;
using Infra.WebAdapter;
using UnityEngine;

public class RewardFactoryService : MonoBehaviour, IRewardFactoryService
{
    private void Awake()
    {
        if (FindObjectsOfType<RewardFactoryService>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        ServiceLocator.Instance.RegisterService<IRewardFactoryService>(this);
    }

    public async UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson reward)
    {
        switch (reward.type)
        {
            case "gold":
                var currency = JsonUtility.FromJson<CurrencyReward>(reward.body);
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(reward.type),
                    value = $"{currency.amount} Gold"
                };

            case "profilePicture":
                var picture = JsonUtility.FromJson<ProfilePictureReward>(reward.body);
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(reward.type),
                    value = $"{picture.id} picture"
                };

            case "profileBackground":
                var background = JsonUtility.FromJson<ProfileBackgroundReward>(reward.body);
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(reward.type),
                    value = $"{background.id} background"
                };

            case "profileAvatar":
                var avatar = JsonUtility.FromJson<ProfileAvatarReward>(reward.body);
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(reward.type),
                    value = $"{avatar.id} avatar"
                };

            case "powerup":
                var powerup = JsonUtility.FromJson<PowerUpReward>(reward.body);
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(powerup.powerup),
                    value = $"Power Up {GetPowerUpName(powerup.powerup)} x {powerup.quantity}"
                };

            default:
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(reward.type),
                    value = $"default"
                };
        }
    }
    
    private string GetPowerUpName(string powerup)
    {
        return powerup switch
        {
            "POWERUP_RAY" => "Ray",
            "POWERUP_ICE" => "Ice",
            "POWERUP_LEAD" => "Lead",
            "POWERUP_JOKER" => "Joker",
            _ => "Unknown"
        };
    }
}

[System.Serializable]
public class CurrencyReward
{
    public int amount;
}

[System.Serializable]
public class ProfilePictureReward
{
    public int id;
}

[System.Serializable]
public class ProfileBackgroundReward
{
    public int id;
}

[System.Serializable]
public class ProfileAvatarReward
{
    public int id;
}

[System.Serializable]
public class PowerUpReward
{
    public string powerup;
    public int quantity;
}


public interface IRewardFactoryService
{
    UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson regard);
}