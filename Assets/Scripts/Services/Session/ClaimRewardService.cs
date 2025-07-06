using Cysharp.Threading.Tasks;

public class ClaimRewardService : IClaimRewardService
{
    public async UniTask<RewardDataJson> ClaimReward(RewardData rewardData)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        return await player.AddReward(rewardData);
    }
}

public interface IClaimRewardService
{
    UniTask<RewardDataJson> ClaimReward(RewardData rewardData);
}