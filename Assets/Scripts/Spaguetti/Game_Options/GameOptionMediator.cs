using Presentation.Tutorial;
using UnityEngine;
using UnityEngine.UI;

public class GameOptionMediator : MonoBehaviour
{
    [SerializeField] private Button playSinglePlayerButton;
    [SerializeField] private Button playLocalPvPButton;
    [SerializeField] private int singlePlayerSceneIndex;

    // Set before leaving the menu, so a double tap cannot queue two splash transitions.
    private bool _isNavigating;

    private void Start()
    {
        playSinglePlayerButton.onClick.AddListener(PlaySinglePlayer);
        playLocalPvPButton.onClick.AddListener(PlayLocalPvP);
    }

    /// <summary>
    /// First Play goes straight to the lesson: no Matching interstitial, no ad, no Lobby. The
    /// transient flag is written here and consumed by <see cref="GameManager"/>; the normal path
    /// clears it as a belt-and-braces measure so it can never leak into a match.
    /// </summary>
    private void PlaySinglePlayer()
    {
        Debug.Log("Play Single Player");
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            var storage = ServiceLocator.Instance.GetService<IStorageData>();
            var tutorialCompleted = storage.LoadDataGlobal(GlobalData.TutorialCompleted) == "true";

            if (!tutorialCompleted)
            {
                TutorialResume.Clear();
                storage.SaveDataGlobal(GlobalData.TutorialMode, "1");
                Debug.Log("[Tutorial] first Play -> Game in tutorial mode (Matching and Lobby skipped).");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
                return;
            }

            storage.SaveDataGlobal(GlobalData.TutorialMode, string.Empty);
            storage.SaveDataGlobal(GlobalData.LocalPvP, string.Empty);
            storage.SaveDataGlobal(GlobalData.GameMode, GameMode.SinglePlayer.ToString());
            UnityEngine.SceneManagement.SceneManager.LoadScene(singlePlayerSceneIndex);
        });
    }

    /// <summary>
    /// Second local human instead of the AI: no Matching interstitial, no ad, no Lobby, exactly like
    /// the tutorial route. The power-up selection lives in its own screen (<c>PvPPowerUps</c>)
    /// reached through the splash, so the menu never hosts an overlay; that screen writes
    /// <see cref="GlobalData.LocalPvP"/> once the owner confirms.
    /// </summary>
    private void PlayLocalPvP()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        Debug.Log("Play Local PvP");
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(
            () => UnityEngine.SceneManagement.SceneManager.LoadScene("PvPPowerUps"));
    }
}
