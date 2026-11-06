#if UNITY_EDITOR
using Infra.Persistence;
using UnityEditor;
using UnityEngine;

namespace MatchBall.Dev.EditorTools
{
    /// <summary>
    /// QA helper: clears local BallPass progress (level, experience and claimed flags) so the pass can
    /// be played and claimed again from level 1. Wallet, inventory and every other key stay untouched.
    /// </summary>
    public static class BallPassDevReset
    {
        private const string MenuItem = "Tools/MatchBall/Dev/Reset Ball Pass";

        [MenuItem(MenuItem)]
        private static void Reset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning(
                    $"[BallPassDev] Reset skipped while Play mode is running or about to start. Stop Play, run {MenuItem} again, " +
                    "then enter Play from Login or Assets/Scenes/Dev/DevGame.unity to see level 1; the open BallPass UI never refreshes by itself.");
                return;
            }

            LogState("before");
            BallPassLocalStore.ResetProgress();
            LogState("after");

            Debug.Log("[BallPassDev] Rewrote playerBallPass only; GOLD, inventory and the other keys were not touched.");
        }

        private static void LogState(string moment)
        {
            var prefix = PlayerPrefs.HasKey(BallPassLocalStore.Key) ? string.Empty : "no saved pass -> zero state; ";
            Debug.Log($"[BallPassDev] {moment}: {prefix}{BallPassLocalStore.DescribeCurrentState()}");
        }
    }
}
#endif
