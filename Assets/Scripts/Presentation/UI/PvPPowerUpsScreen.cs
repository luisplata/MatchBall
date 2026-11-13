using System;
using Cysharp.Threading.Tasks;
using Presentation.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Presentation.UI
{
    /// <summary>
    /// Intermediate screen for local PvP. Shows the account owner's profile and the shared power-up
    /// selector full screen, then starts the match or returns to the menu.
    /// </summary>
    /// <remarks>
    /// Replaces the modal that used to open over MainMenu: the selector is the same prefab, but the
    /// container is a screen of its own, reached through the global splash like every other route.
    /// This scene owns the navigation flags, so "Volver" never writes anything.
    /// </remarks>
    public class PvPPowerUpsScreen : MonoBehaviour
    {
        [SerializeField] private ShowProfileInLobby showProfileInLobby;
        [SerializeField] private PowerUpsSelectionPanelView powerUpsSelectionPanel;
        [SerializeField] private float profileHoldSeconds = 1.2f;

        private bool _isNavigating;

        private void Start()
        {
            // The splash is already opaque when this scene loads: fade it out and only then let the
            // presentation start, so nothing appears behind it.
            ServiceLocator.Instance.GetService<ITransitionService>()
                .HideSplashScreen(() => { RunAsync().Forget(); });
        }

        private async UniTaskVoid RunAsync()
        {
            if (showProfileInLobby != null)
            {
                showProfileInLobby.Configure();
                showProfileInLobby.ShowProfile();
            }

            if (profileHoldSeconds > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(profileHoldSeconds));
            }

            // Same order as the Lobby: the profile leaves before the selector fades in.
            if (showProfileInLobby != null)
            {
                showProfileInLobby.HideProfile();
            }

            // A missing panel would leave the player stranded on this screen, so treat it as a
            // straight confirmation instead of waiting forever.
            var confirmed = powerUpsSelectionPanel != null
                ? await powerUpsSelectionPanel.ShowAndWaitAsync()
                : true;

            if (confirmed)
            {
                var storage = ServiceLocator.Instance.GetService<IStorageData>();
                storage.SaveDataGlobal(GlobalData.TutorialMode, string.Empty);
                storage.SaveDataGlobal(GlobalData.LocalPvP, "1");
                TutorialResume.Clear();
                GoTo("Game");
                return;
            }

            GoTo("MainMenu");
        }

        private void GoTo(string sceneName)
        {
            if (_isNavigating)
            {
                return;
            }

            _isNavigating = true;
            ServiceLocator.Instance.GetService<ITransitionService>()
                .ShowSplashScreen(() => SceneManager.LoadScene(sceneName));
        }
    }
}
