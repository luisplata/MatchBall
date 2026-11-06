using System;
using App.Reward;
using Bootstrap;
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

    public async UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson reward)
    {
        var display = ResolveCatalog().Describe(reward.type, reward.body);
        return new RewardConfigData
        {
            image = await AssetFactory<Sprite>.LoadByKeyAsync(display.IconKey),
            value = display.Label
        };
    }

    private static IRewardCatalog ResolveCatalog()
    {
        if (ServiceLocator.Instance.TryGetService<IRewardCatalog>(out var catalog) && catalog != null) return catalog;

        throw new InvalidOperationException(
            "IRewardCatalog is not registered. Start Play from Login or the DevGame prelude.");
    }
}

public interface IRewardFactoryService
{
    UniTask<RewardConfigData> GetRewardConfigData(RewardDataJson regard);
}
