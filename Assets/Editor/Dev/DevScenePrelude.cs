#if UNITY_EDITOR
using Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MatchBall.Dev.EditorTools
{
    /// <summary>
    /// Arms EditorSceneManager.playModeStartScene with the DevGame prelude only when Play is pressed
    /// while Game.unity is the active scene. The redirect is global Editor state, so it is armed as
    /// late as possible (ExitingEditMode) and always cleared when returning to edit mode.
    /// </summary>
    [InitializeOnLoad]
    public static class DevScenePrelude
    {
        private const string MenuRoot = "Tools/MatchBall/Dev/";
        private const string EnabledKey = "MatchBall.Dev.PreludeEnabled";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string PreludeScenePath = "Assets/Scenes/Dev/DevGame.unity";

        private static bool _manualArmed;

        static DevScenePrelude()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static bool IsEnabled => EditorPrefs.GetBool(EnabledKey, true);

        private static bool DomainReloadEnabled =>
            !EditorSettings.enterPlayModeOptionsEnabled ||
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) == 0;

        private static SceneAsset PreludeAsset => AssetDatabase.LoadAssetAtPath<SceneAsset>(PreludeScenePath);

        private const string DomainReloadWarning =
            "[DevPrelude] Domain Reload is disabled -> Play will NOT redirect to the prelude " +
            "(why: the ServiceLocator registry would survive between Play sessions and every registration " +
            "would assert 'already registered'). Enable Edit > Project Settings > Editor > Enter Play Mode " +
            "Settings > Reload Domain, or launch Play normally without the prelude.";

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Arm();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _manualArmed = false;
                    Clear();
                    break;
            }
        }

        private static void Arm()
        {
            var prelude = PreludeAsset;

            if (_manualArmed)
            {
                if (prelude != null)
                {
                    EditorSceneManager.playModeStartScene = prelude;
                    Debug.Log(
                        $"[DevPrelude] Manual Play -> starts at {PreludeScenePath} (why: Tools/MatchBall/Dev/Play From Prelude)");
                    return;
                }

                Debug.LogError($"[DevPrelude] {PreludeScenePath} not found -> manual prelude Play aborted.");
                Clear();
                return;
            }

            if (!IsEnabled)
            {
                Clear();
                return;
            }

            var activePath = SceneManager.GetActiveScene().path;
            if (activePath != GameScenePath)
            {
                Clear();
                return;
            }

            if (prelude == null)
            {
                Debug.LogWarning($"[DevPrelude] {PreludeScenePath} not found -> Play starts at {activePath}.");
                Clear();
                return;
            }

            if (!DomainReloadEnabled)
            {
                Debug.LogWarning(DomainReloadWarning);
                Clear();
                return;
            }

            EditorSceneManager.playModeStartScene = prelude;
            Debug.Log(
                $"[DevPrelude] Active scene {activePath} -> Play starts at {prelude.name} " +
                "(why: dev harness; disable in Tools/MatchBall/Dev)");
        }

        private static void Clear()
        {
            if (EditorSceneManager.playModeStartScene != null)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        [MenuItem(MenuRoot + "Game Prelude Enabled")]
        private static void TogglePrelude()
        {
            EditorPrefs.SetBool(EnabledKey, !IsEnabled);
            Debug.Log($"[DevPrelude] Game prelude {(IsEnabled ? "enabled" : "disabled")}.");
        }

        [MenuItem(MenuRoot + "Game Prelude Enabled", true)]
        private static bool TogglePreludeValidate()
        {
            Menu.SetChecked(MenuRoot + "Game Prelude Enabled", IsEnabled);
            return true;
        }

        [MenuItem(MenuRoot + "Play From Prelude")]
        private static void PlayFromPrelude()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var prelude = PreludeAsset;
            if (prelude == null)
            {
                Debug.LogError($"[DevPrelude] {PreludeScenePath} not found -> cannot play from the prelude.");
                return;
            }

            if (!DomainReloadEnabled)
            {
                Debug.LogWarning(DomainReloadWarning);
                return;
            }

            _manualArmed = true;
            EditorSceneManager.playModeStartScene = prelude;
            EditorApplication.isPlaying = true;
        }

        [MenuItem(MenuRoot + "Reset Seeded Inventory")]
        private static void ResetSeededInventory()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[DevPrelude] Stop Play mode before resetting the seeded inventory.");
                return;
            }

            Debug.Log(DevGameBootstrap.ResetSeededInventory());
        }
    }
}
#endif
