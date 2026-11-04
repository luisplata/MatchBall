#if UNITY_EDITOR
using System;
using System.Linq;
using App.Ports;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Bootstrap;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Dev
{
    /// <summary>
    /// Editor-only prelude seeder. Lives in the DevGame prelude scene, composes the services that
    /// usually come from Login/MainMenu/Lobby, seeds config + inventory + power-up selection and then
    /// loads Game by name. Never referenced by a player build (entire file is editor-only and the
    /// prelude scene is not in the build settings).
    /// </summary>
    public class DevGameBootstrap : MonoBehaviour
    {
        private const string InventoryKey = "PlayerInventory";
        private const string BackupExistedKey = "MatchBall.Dev.PreludeInventoryBackupExisted";
        private const string BackupJsonKey = "MatchBall.Dev.PreludeInventoryBackupJson";

        // Serialized prelude refs: used only to PROMOTE survivor identity, never to register anything.
        [SerializeField] private PlayerSession preludeSession;
        [SerializeField] private TransitionService preludeTransition;
        [SerializeField] private string[] seedIds = { "POWERUP_ICE", "POWERUP_JOKER", "POWERUP_LEAD", "POWERUP_RAY" };
        [SerializeField] private int seedQuantity = 3;

        private string _step = "awake";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BootAsync().Forget();
        }

        private async UniTaskVoid BootAsync()
        {
            // Every sibling Start (GameInstaller.Install, PlayerSession.Awake side effects) must have run.
            await UniTask.Yield();

            try
            {
                _step = "preflight";
                if (preludeSession == null || preludeTransition == null)
                {
                    throw new Exception("serialized prelude references (session/transition) are not assigned");
                }

                Require<IEventBus>("IEventBus");
                var inventoryRepository = Require<IInventoryRepository>("IInventoryRepository");
                var powerUps = Require<IPowerUpsUseCase>("IPowerUpsUseCase");
                var config = Require<IGameConfigurationUseCase>("IGameConfigurationUseCase");
                var storage = Require<IStorageData>("IStorageData");
                Require<ITransitionService>("ITransitionService");

                _step = "config";
                config.LoadConfig();
                if (config.GetConfig() == null)
                {
                    throw new Exception("GetConfig() returned null after LoadConfig()");
                }

                _step = "inventory";
                var inventoryState = "reused";
                var inventory = await inventoryRepository.GetPlayerInventory();
                if (inventory.PowerUps.Count == 0)
                {
                    BackupInventoryOnce();
                    foreach (var id in seedIds)
                    {
                        await inventoryRepository.AddPowerUp(id, seedQuantity);
                    }

                    inventoryState = "seeded";
                }

                // Must precede selection: SyncPowerUps rebuilds _powerUpsSelected from _powerUpsData,
                // so selecting before loading is silently erased.
                _step = "powerups";
                await powerUps.LoadPowerUps();

                _step = "selection";
                foreach (var id in seedIds)
                {
                    powerUps.PowerUpSelected(id, true);
                }

                _step = "gamemode";
                storage.SaveDataGlobal(GlobalData.GameMode, GameMode.SinglePlayer.ToString());

                _step = "load";
                await SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);

                _step = "summary";
                await LogSummary(inventoryRepository, powerUps, inventoryState);

                Destroy(gameObject);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DevBootstrap] FAILED at {_step}: {e.Message}");
            }
        }

        private async UniTask LogSummary(IInventoryRepository inventoryRepository, IPowerUpsUseCase powerUps,
            string inventoryState)
        {
            var inventory = await inventoryRepository.GetPlayerInventory();
            var quantities = string.Join(" ", seedIds.Select(id =>
            {
                var entry = inventory.PowerUps.FirstOrDefault(p => p.Id == id);
                return $"{Label(id)}={(entry == null ? 0 : entry.Quantity)}";
            }));

            var selected = powerUps.GetSelectedPowerUps().Count;
            var sessionIsPrelude = ServiceLocator.Instance.TryGetService<IPlayerSessionService>(out var liveSession)
                                   && ReferenceEquals(liveSession, preludeSession);
            var transitionIsPrelude = ServiceLocator.Instance.TryGetService<ITransitionService>(out var liveTransition)
                                      && ReferenceEquals(liveTransition, preludeTransition);

            Debug.Log($"[DevBootstrap] ready config=ok inventory={inventoryState} {quantities} selected={selected} " +
                      $"session={(sessionIsPrelude ? "prelude" : "other")} " +
                      $"transition={(transitionIsPrelude ? "prelude" : "other")}");
        }

        private static string Label(string id)
        {
            const string prefix = "POWERUP_";
            return id.StartsWith(prefix, StringComparison.Ordinal) ? id.Substring(prefix.Length) : id;
        }

        private static T Require<T>(string name)
        {
            if (!ServiceLocator.Instance.TryGetService<T>(out var service) || service == null)
            {
                throw new Exception($"service {name} is not registered");
            }

            return service;
        }

        private static void BackupInventoryOnce()
        {
            if (EditorPrefs.HasKey(BackupExistedKey))
            {
                return;
            }

            var existed = PlayerPrefs.HasKey(InventoryKey);
            EditorPrefs.SetBool(BackupExistedKey, existed);
            EditorPrefs.SetString(BackupJsonKey, existed ? PlayerPrefs.GetString(InventoryKey) : string.Empty);
        }

        /// <summary>
        /// Restores the pre-seed PlayerInventory snapshot taken by the first seeding run.
        /// Used by the Tools/MatchBall/Dev/Reset Seeded Inventory menu.
        /// </summary>
        public static string ResetSeededInventory()
        {
            if (!EditorPrefs.HasKey(BackupExistedKey))
            {
                return "[DevBootstrap] reset: no seeded-inventory backup found; nothing to restore";
            }

            string message;
            if (EditorPrefs.GetBool(BackupExistedKey, false))
            {
                var json = EditorPrefs.GetString(BackupJsonKey, string.Empty);
                PlayerPrefs.SetString(InventoryKey, json);
                message = $"[DevBootstrap] reset: PlayerInventory restored to its pre-seed snapshot ({json.Length} chars)";
            }
            else
            {
                PlayerPrefs.DeleteKey(InventoryKey);
                message = "[DevBootstrap] reset: PlayerInventory did not exist before seeding -> key deleted";
            }

            PlayerPrefs.Save();
            EditorPrefs.DeleteKey(BackupExistedKey);
            EditorPrefs.DeleteKey(BackupJsonKey);
            return message;
        }
    }
}
#endif
