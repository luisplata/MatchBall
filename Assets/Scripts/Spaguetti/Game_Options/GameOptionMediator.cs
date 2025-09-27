using UnityEngine;
using UnityEngine.UI;

public class GameOptionMediator : MonoBehaviour
{
    [SerializeField] private Button playSinglePlayerButton;
    [SerializeField] private int singlePlayerSceneIndex;

    private void Start()
    {
        playSinglePlayerButton.onClick.AddListener(PlaySinglePlayer);
    }

    private void PlaySinglePlayer()
    {
        Debug.Log("Play Single Player");
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            ServiceLocator.Instance.GetService<IStorageData>().SaveDataGlobal(GlobalData.GameMode, GameMode.SinglePlayer.ToString());
            UnityEngine.SceneManagement.SceneManager.LoadScene(singlePlayerSceneIndex);
        });
    }
}

