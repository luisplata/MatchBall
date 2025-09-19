using Cysharp.Threading.Tasks;
using UnityEngine;

public class PlayerListOfRewards : MonoBehaviour
{
    public async UniTask Configure()
    {
        var rewards = await ServiceLocator.Instance.GetService<IHttpRequestsCustom>().PlayerRewardApi.GetRewards();
        foreach (var rewardData in rewards)
        {
            //Debug.Log(rewardData.level);
            //Debug.Log(rewardData.reward);
        }
    }
}