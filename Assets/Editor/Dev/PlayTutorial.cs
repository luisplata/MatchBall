#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MatchBall.Dev.EditorTools
{
    /// <summary>
    /// QA helper: arms the transient tutorial flag and enters Play on Game.
    /// <para>
    /// The lesson is gated on <see cref="GlobalData.TutorialMode"/> being "1", which the lobby's Play
    /// button and the Instruccions button write. The DevGame prelude does not, so pressing Play with
    /// Game.unity open never shows the tutorial; this menu writes the flag for you.
    /// </para>
    /// </summary>
    public static class PlayTutorial
    {
        private const string MenuItem = "Tools/MatchBall/Dev/Play Tutorial";
        private const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem(MenuItem, false, 1)]
        private static void Play()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Tutorial] Stop Play mode before launching the tutorial.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PlayerPrefs.SetString(GlobalData.TutorialMode.ToString(), "1");
            PlayerPrefs.Save();

            // The prelude only redirects to DevGame when Play starts from Game, and it is what
            // bootstraps the service locator, so Game has to be the active scene before Play.
            EditorSceneManager.OpenScene(GameScenePath);
            EditorApplication.EnterPlaymode();
        }
    }
}
#endif
