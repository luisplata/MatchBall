using System;
using App.UseCase.PassBall;
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

            var bp = await ServiceLocator.Instance.GetService<IPassBallUseCase>().GetPlayerBattlePass();
            var pg = await ServiceLocator.Instance.GetService<IPassBallUseCase>().GetPlayerRewards();

            try
            {
                await listOfRewards.Configure(pg, bp);
                await playerListOfRewards.Configure();
            }
            catch (Exception e)
            {
                await ServiceLocator.Instance.GetService<IMessageService>()
                    .ErrorMessage($"Error getting rewards, load dummys: {e.Message}", 5);
            }

            await ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
            {
                //Debug.Log("Hide splash screen");
                listOfRewards.StartAnimation();
            });
        }
        catch (Exception e)
        {
            await ServiceLocator.Instance.GetService<IMessageService>()
                .ErrorMessage($"Error getting player battle pass: {e.Message}", 5);
            await ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
            {
                //Debug.Log("Hide splash screen");
                listOfRewards.StartAnimation();
            });
        }
    }

    private void ReturnToMenu()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            SceneManager.LoadScene("MainMenu");
        });
    }
}