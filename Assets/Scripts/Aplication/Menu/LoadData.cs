using Cysharp.Threading.Tasks;
using UnityEngine;

public class LoadData : MonoBehaviour
{
    private async void Start()
    {
        var playerData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        if (playerData != null)
        {
            // Load player data
            //Debug.Log("Player data loaded");
            await ServiceLocator.Instance.GetService<IMenuUi>().UpdatePlayerData(playerData);
            ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen();
        }
        else
        {
            //Debug.Log("Player data not found");
        }
    }
}