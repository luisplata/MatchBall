#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MatchBall.Dev.EditorTools
{
    /// <summary>
    /// QA helper: removes both tutorial keys so the next Play exercises the first-run gate again.
    /// </summary>
    public static class ResetTutorial
    {
        private const string MenuItem = "Tools/MatchBall/Dev/Reset Tutorial";

        [MenuItem(MenuItem)]
        private static void Reset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Tutorial] Stop Play mode before resetting the tutorial flags.");
                return;
            }

            PlayerPrefs.DeleteKey(GlobalData.TutorialMode.ToString());
            PlayerPrefs.DeleteKey(GlobalData.TutorialCompleted.ToString());
            PlayerPrefs.Save();
            Debug.Log("[Tutorial] removed TutorialMode and TutorialCompleted from PlayerPrefs.");
        }
    }
}
#endif
