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
        const int maxRetries = 3;
        int attempt = 0;

        loginButton.interactable = false;

        ServiceLocator.Instance.GetService<IMessageService>()
            .ShowMessage("Logging in...", 5)
            .Forget();

        while (attempt < maxRetries)
        {
            try
            {
                attempt++;

                await ServiceLocator.Instance.GetService<IPlayerSessionService>().Login();

                // ✅ Login OK
                loginButton.gameObject.SetActive(false);
                var playerName = ServiceLocator.Instance
                    .GetService<IPlayerSessionService>()
                    .GetPlayerSessionData().playerName;

                ServiceLocator.Instance.GetService<IMessageService>()
                    .ShowMessage($"Welcome {playerName}", 5).Forget();

                await UniTask.Delay(1000);
                await ServiceLocator.Instance.GetService<ITransitionService>()
                    .ShowSplashScreen(StartGame);

                return; // Salimos si tuvo éxito
            }
            catch (Exception e)
            {
                Debug.LogError($"[Login] Attempt {attempt} failed: {e}");

                if (attempt >= maxRetries)
                {
                    // ❌ Se agotaron los intentos
                    ServiceLocator.Instance.GetService<IMessageService>()
                        .ErrorMessage("Error logging in, please try again later.")
                        .Forget();
                }
                else
                {
                    // ⏳ Espera antes del retry (exponencial)
                    int delayMs = attempt * 2000; // 2s, 4s, 6s...
                    await UniTask.Delay(delayMs);
                }
            }
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