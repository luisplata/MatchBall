using Cysharp.Threading.Tasks;
using Presentation.Tutorial;
using Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

public class GameOptionMediator : MonoBehaviour
{
    [SerializeField] private Button playSinglePlayerButton;
    [SerializeField] private Button playLocalPvPButton;
    [SerializeField] private int singlePlayerSceneIndex;
    [SerializeField] private PowerUpsSelectionPanelView powerUpsSelectionPanel;

    // Set before the first await of the PvP route, so a double tap on the mobile button cannot open
    // the selector twice nor queue two navigations. Every exit that stays in MainMenu clears it; the
    // one that leaves keeps it set, because the scene load is already on its way.
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
    /// the tutorial route. The transient flag is written here and consumed by
    /// <see cref="GameManager"/> on boot, so a second launch is a normal match.
    /// Both boards share the owner's power-up selection, so the selector opens over MainMenu and the
    /// match only starts after "Listo"; "Volver" leaves MainMenu with nothing written.
    /// </summary>
    private void PlayLocalPvP()
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        Debug.Log("Play Local PvP");
        RunLocalPvPAsync().Forget();
    }

    private async UniTaskVoid RunLocalPvPAsync()
    {
        // No panel wired in this scene keeps the previous straight-to-the-match behaviour.
        if (powerUpsSelectionPanel == null)
        {
            NavigateToGame();
            return;
        }

        // The panel answers after its close fade, so the splash starts with the panel already gone.
        var confirmed = await powerUpsSelectionPanel.ShowAndWaitAsync();

        if (!confirmed)
        {
            _isNavigating = false;
            return;
        }

        NavigateToGame();
    }

    private void NavigateToGame()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(() =>
        {
            var storage = ServiceLocator.Instance.GetService<IStorageData>();
            storage.SaveDataGlobal(GlobalData.TutorialMode, string.Empty);
            storage.SaveDataGlobal(GlobalData.LocalPvP, "1");
            TutorialResume.Clear();
            Debug.Log("[LocalPvP] boot -> Game (Matching, ad and Lobby skipped).");
            UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
        });
    }
}
