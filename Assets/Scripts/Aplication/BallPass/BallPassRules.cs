using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BallPassRules : MonoBehaviour
{
    [SerializeField] private Button returnButton;
    [SerializeField] private ListOfRewards listOfRewards;
    [SerializeField] private PlayerListOfRewards playerListOfRewards;

    async void Start()
    {
        try
        {
            returnButton.onClick.AddListener(ReturnToMenu);

            var bp = await ServiceLocator.Instance.GetService<IHttpRequestsCustom>()
                .PlayerRewardApi.GetPlayerBattlePass();

            try
            {
                await listOfRewards.Configure(await ServiceLocator.Instance.GetService<IHttpRequestsCustom>()
                    .PlayerRewardApi.GetRewards(), bp);
                await playerListOfRewards.Configure();
            }
            catch (Exception e)
            {
                ServiceLocator.Instance.GetService<IMessageService>()
                    .ErrorMessage($"Error getting rewards, load dummys: {e.Message}", 5);
                await CreateDummyRewards();
            }

            ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
            {
                //Debug.Log("Hide splash screen");
                listOfRewards.StartAnimation();
            });
        }
        catch (Exception e)
        {
            ServiceLocator.Instance.GetService<IMessageService>()
                .ErrorMessage($"Error getting player battle pass: {e.Message}", 5);
            await CreateDummyRewards();
            ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
            {
                //Debug.Log("Hide splash screen");
                listOfRewards.StartAnimation();
            });
        }
    }

    private async UniTask CreateDummyRewards()
    {
        var rewards = new List<RewardData>
        {
            new()
            {
                id = 1,
                level = 1,
                reward = "Reward 1",
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            },
            new()
            {
                id = 2,
                level = 2,
                reward = "Reward 2",
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            },
            new()
            {
                id = 3,
                level = 3,
                reward = "Reward 3",
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            },
            new()
            {
                id = 4,
                level = 4,
                reward = "Reward 4",
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            },
            new()
            {
                id = 5,
                level = 5,
                reward = "Reward 5",
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            }
        };
        listOfRewards.Configure(rewards, new PlayerBattlePass()).Forget();
    }

    private void ReturnToMenu()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            SceneManager.LoadScene("MainMenu");
        });
    }
}