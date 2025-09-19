using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ListOfRewards : MonoBehaviour
{
    [SerializeField] private Reward rewardPrefab;
    [SerializeField] private Transform rewardsContainer;
    private List<Reward> _rewards = new();

    public List<Reward> RewardList => _rewards;

    private PlayerBattlePass _playerBattlePass;

    public async UniTask Configure(List<RewardData> rewards, PlayerBattlePass bp)
    {
        _playerBattlePass = bp;
        //delete all rewards
        foreach (var reward in _rewards)
        {
            Destroy(reward.gameObject);
        }

        _rewards.Clear();

        foreach (var reward in rewards)
        {
            var r = Instantiate(rewardPrefab, rewardsContainer);
            await r.Configure(reward);
            _rewards.Add(r);
        }
    }


    public async void StartAnimation()
    {
        foreach (var reward in _rewards)
        {
            if (reward.RewardData.level < _playerBattlePass.level)
            {
                await reward.AnimToReceived(1);

                foreach (var playerReward in _playerBattlePass.playerRewards.Where(playerReward =>
                             playerReward.rewardId == reward.RewardData.id))
                {
                    reward.ConfigureClaimed(playerReward.claimed);
                }
            }

            if (reward.RewardData.level == _playerBattlePass.level)
            {
                await reward.AnimToHalfReceived(_playerBattlePass.experience, 1);
            }
        }
    }
}

[Serializable]
public class RewardData
{
    public int id;
    public int level;
    public string reward;
    public int experience;
    public DateTime created_at;
    public DateTime updated_at;
}

[Serializable]
public class RewardDataList
{
    public List<RewardData> rewards;
}