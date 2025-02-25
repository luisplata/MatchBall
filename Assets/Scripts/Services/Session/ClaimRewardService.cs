using Cysharp.Threading.Tasks;

public class ClaimRewardService : IClaimRewardService
{
    public async UniTask ClaimReward(RewardData rewardData)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        await player.AddReward(rewardData);
    }
}

public interface IClaimRewardService
{
    UniTask ClaimReward(RewardData rewardData);
}