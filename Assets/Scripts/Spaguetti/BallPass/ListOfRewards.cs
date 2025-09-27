using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Presentation.Game;
using UnityEngine;

public class ListOfRewards : MonoBehaviour
{
    [SerializeField] private Reward rewardPrefab;
    [SerializeField] private Transform rewardsContainer;
    private List<Reward> _rewards = new();

    public List<Reward> RewardList => _rewards;

    private PlayerPassBall _playerBattlePass;

    public async UniTask Configure(List<RewardData> rewards, PlayerPassBall bp)
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
            if (reward.RewardData.Level < _playerBattlePass.Level)
            {
                await reward.AnimToReceived(1);

                foreach (var playerReward in _playerBattlePass.PlayerRewards.Where(playerReward =>
                             playerReward.Level == reward.RewardData.Level))
                {
                    reward.ConfigureClaimed(playerReward.Claimed);
                }
            }

            if (reward.RewardData.Level == _playerBattlePass.Level)
            {
                await reward.AnimToHalfReceived(_playerBattlePass.Experience, 1);
            }
        }
    }
}