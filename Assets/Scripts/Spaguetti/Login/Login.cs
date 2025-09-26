using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Login : MonoBehaviour
{
    [SerializeField] private Button loginButton;
    [SerializeField] private PlayableDirector openingCinematic;

    private void Awake()
    {
        loginButton.gameObject.SetActive(false);
        loginButton.onClick.AddListener(LoginGoogle);
        Invoke(nameof(Configure), (float)openingCinematic.duration);
    }

    public void Configure()
    {
        loginButton.gameObject.SetActive(true);
        openingCinematic.gameObject.SetActive(false);
        ServiceLocator.Instance.GetService<IMusicController>().PlayMusic("Mystery");
    }

    private async void LoginGoogle()
    {
        try
        {
            loginButton.interactable = false;
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage("Logging in...", 5);
            await ServiceLocator.Instance.GetService<IPlayerSessionService>().Login();
            loginButton.gameObject.SetActive(false);
            var playerName = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData()
                .playerName;
            ServiceLocator.Instance.GetService<IMessageService>().ShowMessage($"Welcome {playerName}", 5);
            await UniTask.Delay(1000);
            ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(StartGame);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage($"Error logging in try again later: {e.Message}");
        }

        loginButton.interactable = true;
    }

    public async void Logout()
    {
        await ServiceLocator.Instance.GetService<IPlayerSessionService>().Logout();
        loginButton.gameObject.SetActive(true);
    }

    public void ShowInfo()
    {
        var playerSessionData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        //Debug.Log($"PlayerID: {playerSessionData.playerId}");
        //Debug.Log($"PlayerName: {playerSessionData.playerName}");
        //Debug.Log($"PlayerEmail: {playerSessionData.playerEmail}");
        //Debug.Log($"PlayerPhotoUrl: {playerSessionData.playerPhotoUrl}");

        ServiceLocator.Instance.GetService<IMessageService>().ShowMessage($"PlayerID: {playerSessionData.playerId}");
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }
}