using Cysharp.Threading.Tasks;
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

    public async UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson regard)
    {
        switch (regard.type)
        {
            case "gold":
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync(regard.type),
                    value = $"{regard.count} Gold"
                };

            case "profilePicture":
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadAsync("ballpass", regard.type + "_" + regard.count),
                    value = "Profile Picture"
                };

            case "profileBackground":
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadAsync("ballpass", regard.type + "_" + regard.count),
                    value = "Profile Background"
                };

            case "profileAvatar":
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadAsync("ballpass", regard.type + "_" + regard.count),
                    value = "Profile Avatar"
                };

            case "powerup":
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadAsync("ballpass", regard.type + "_" + regard.count),
                    value = $"{regard.count} Power-up"
                };

            default:
                Debug.LogError($"Unknown reward type: {regard.type}");
                return new RewardConfigData
                {
                    image = await AssetFactory<Sprite>.LoadByKeyAsync("default"),
                    value = "Unknown Reward"
                };
        }
    }
}

public interface IRewardFactoryService
{
    UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson regard);
}