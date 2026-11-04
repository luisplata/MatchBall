using System;
using System.Collections.Generic;
using System.Linq;
using App.Ports;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Cysharp.Threading.Tasks;
using Presentation.Game;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Economy;
using Unity.Services.Friends;
using Unity.Services.RemoteConfig;
using UnityEngine;
using Random = UnityEngine.Random;

public class PlayerSession : MonoBehaviour, IPlayerSessionService, IStorageData, IGlobalSessionService
{
    [SerializeField] private string environmentName;
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    private PlayerSessionDataCustom _playerSessionData;
    private bool _readyToSignIn;
    private PowerUpsConfiguration _powerUpsConfigurationInstance;


    /// <summary>
    /// Indica si la sesión está lista para permitir el cambio de escena.
    /// </summary>
    public bool IsSessionReadyForSceneChange()
    {
        // Considera la sesión lista si está inicializada, logueada y los datos principales están cargados.
        // Puedes agregar más validaciones según necesidades (por ejemplo, que no haya operaciones pendientes).
        return _readyToSignIn && _playerSessionData != null && !string.IsNullOrEmpty(_playerSessionData.playerId) &&
               !string.IsNullOrEmpty(_playerSessionData.playerName);
    }

    public struct userAttributes
    {
    }

    public struct appAttributes
    {
    }

    private void Awake()
    {
        if (FindObjectsOfType<PlayerSession>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        if (ServiceLocator.Instance.RegisterService<IPlayerSessionService>(this) &&
            ServiceLocator.Instance.RegisterService<IStorageData>(this) &&
            ServiceLocator.Instance.RegisterService<IGlobalSessionService>(this))
        {
            // await Initialize();

            _playerSessionData = new PlayerSessionDataCustom();
            DontDestroyOnLoad(gameObject);
            _readyToSignIn = true;
            _powerUpsConfigurationInstance = Instantiate(powerUpsConfiguration);
        }
    }

    private static void SingOut()
    {
        if (!AuthenticationService.Instance.IsSignedIn) return;
        AuthenticationService.Instance.SignOut();
        //Debug.Log("User signed out.");
    }

    public async UniTask Initialize()
    {
        var initializationOptions = new InitializationOptions()
            .SetEnvironmentName(environmentName);

        await UnityServices.InitializeAsync(initializationOptions);
    }

    public async UniTask Login()
    {
        if (!_readyToSignIn)
        {
            await ServiceLocator.Instance.GetService<IMessageService>()
                .ErrorMessage("PlayerSession is not ready to sign in");
            return;
        }

        if (!await SignInAnonymouslyAsync())
        {
            return;
        }

        await UpdatePlayerData(_playerSessionData);

        ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().LoadConfig();
        ServiceLocator.Instance.GetService<IPowerUpsUseCase>().LoadPowerUps().Forget();

        // RemoteConfigService.Instance.FetchCompleted += ApplyRemoteSettings;
        // RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
    }

    private void ApplyRemoteSettings(ConfigResponse obj)
    {
        ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().LoadConfig();
        ServiceLocator.Instance.GetService<IPowerUpsUseCase>().LoadPowerUps().Forget();
    }

    public async UniTask UpdatePlayerData(PlayerSessionDataCustom player)
    {
        // foreach (var (key, value) in player.GetData())
        // {
        //     await SaveData(key, value);
        // }

        _playerSessionData = player;
        // await ServiceLocator.Instance.GetService<ILoginUseCase>().UpdatePlayerNickname(_playerSessionData.playerId, _playerSessionData.playerName);
    }

    public void SaveDataGlobal(GlobalData key, string value)
    {
        PlayerPrefs.SetString(key.ToString(), value);
    }

    public string LoadDataGlobal(GlobalData key)
    {
        return PlayerPrefs.GetString(key.ToString());
    }

    public UniTask Logout()
    {
        AuthenticationService.Instance.SignOut();
        return UniTask.CompletedTask;
    }

    public PlayerSessionDataCustom GetPlayerSessionData()
    {
        return _playerSessionData;
    }

    public async UniTask AddCoins(int coins)
    {
        const string currencyId = "GOLD";
        int currentAmount = PlayerPrefs.GetInt(currencyId, 0);
        int newAmount = currentAmount + coins * 10;
        PlayerPrefs.SetInt(currencyId, newAmount);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Match income: score converts to gold 1:1 (no legacy x10).
    /// </summary>
    public async UniTask AddMatchGold(int amount)
    {
        const string currencyId = "GOLD";
        int currentAmount = PlayerPrefs.GetInt(currencyId, 0);
        int newAmount = currentAmount + amount;
        PlayerPrefs.SetInt(currencyId, newAmount);
        PlayerPrefs.Save();
    }

    public bool PowerUpSelected(string powerUpName, bool isSelected)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = player.GetPowerUps();
        //var powerUpsData = player.GetPowerUpsDummy();
        var powerUpData = powerUpsData.Find(x => x.id == powerUpName);
        powerUpData.selected = isSelected;
        player.powerups_json = JsonUtility.ToJson(new PowerUpsData { powerups = powerUpsData });
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(player).Forget();
        return powerUpData.selected;
    }

    public void AddPowerUpToPlayer(StoreItemDataFromCloud powerUp, int count)
    {
        _playerSessionData.AddPowerUpToPlayer(powerUp, count);
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(_playerSessionData).Forget();
    }

    public bool CheckPowerUp(string powerUpId)
    {
        return _playerSessionData.CheckPowerUp(powerUpId);
    }

    private async UniTask<(bool, PlayerSessionDataCustom)> HasSavedSessionData()
    {
        try
        {
            var playerSessionData =
                JsonUtility.FromJson<PlayerSessionDataCustom>(PlayerPrefs.GetString("playerSessionData"));
            if (playerSessionData == null)
            {
                return (false, null);
            }

            return (true, playerSessionData);
            // var keys = await CloudSaveService.Instance.Data.Player.ListAllKeysAsync();
            // if (keys.Any(itemKey => itemKey.Key == "isCreated"))
            // {
            //     var datas = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
            //     foreach (var data in datas.Where(data => data.Key == "isCreated"))
            //     {
            //         _playerSessionData.isCreated = bool.TryParse(data.Value.ToString(), out var isCreated) && isCreated;
            //         return true;
            //     }
            // }
            //
            // throw new Exception("Data not found");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return (false, null);
        }
    }

    private async UniTask<bool> SignInAnonymouslyAsync(int maxRetries = 3)
    {
        int attempt = 0;

        while (attempt < maxRetries)
        {
            try
            {
                attempt++;
                Debug.Log($"[Auth] Anonymous sign-in attempt {attempt}/{maxRetries}");

                // if (!AuthenticationService.Instance.IsSignedIn)
                // {
                //     await AuthenticationService.Instance.SignInAnonymouslyAsync();
                // }

                // ✅ Initialize friends service (best-effort). Failures here (DNS, network, Unity Services) should
                // not block sign-in — we'll log and continue without friends if initialization fails.
                var friendsInitialized = false;
                try
                {
                    // await FriendsService.Instance.InitializeAsync();
                    friendsInitialized = true;
                }
                catch (RequestFailedException ex)
                {
                    Debug.LogWarning(
                        $"[Auth] FriendsService.InitializeAsync failed: {ex.Message}. Continuing without friends.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Auth] FriendsService.InitializeAsync unexpected error: {ex}");
                }

                if (friendsInitialized)
                {
                    try
                    {
                        // var friends = FriendsService.Instance.Friends;
                        // foreach (var friend in friends)
                        // {
                        //     Debug.Log($"[Friends] {friend}");
                        // }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Auth] Failed reading FriendsService.Instance.Friends: {ex.Message}");
                    }
                }

                // ✅ Load saved session or create new one
                if (await HasSavedSessionData() is (true, var savedSessionData))
                {
                    // var data = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
                    // _playerSessionData.LoadData(data);
                    // Ensure loaded session has required identifiers. CloudSave may have been saved without
                    // playerId or playerName (or they could be empty). In that case, fill with sensible
                    // defaults and persist back to CloudSave so future runs are consistent.
                    var dirty = false;
                    if (string.IsNullOrWhiteSpace(_playerSessionData.playerId))
                    {
                        // _playerSessionData.playerId = AuthenticationService.Instance.PlayerId;
                        _playerSessionData = savedSessionData;
                        Debug.Log($"[Auth] Loaded session missing playerId, set to {_playerSessionData.playerId}");
                        dirty = true;
                    }

                    if (string.IsNullOrWhiteSpace(_playerSessionData.playerName))
                    {
                        int randomSuffix = Random.Range(1000, 9999);
                        string baseName = SystemInfo.deviceUniqueIdentifier;
                        _playerSessionData.playerName = $"{baseName}_{randomSuffix}";
                        Debug.Log(
                            $"[Auth] Loaded session had empty playerName, assigned {_playerSessionData.playerName}");
                        dirty = true;
                    }

                    if (dirty)
                    {
                        // Persist corrected values
                        await UpdatePlayerData(_playerSessionData);
                    }

                    Debug.Log($"[Auth] Loaded existing session for {_playerSessionData.playerName}");
                }
                else
                {
                    // _playerSessionData.playerId = AuthenticationService.Instance.PlayerId;
                    _playerSessionData.playerId = "Anonimus";

                    // Nickname único por dispositivo
                    int randomSuffix = Random.Range(1000, 9999);
                    string baseName = SystemInfo.deviceUniqueIdentifier;
                    _playerSessionData.playerName = $"{baseName}_{randomSuffix}";

                    _playerSessionData.playerEmail = "Anonymous";
                    _playerSessionData.playerPhotoUrl = "Anonymous";
                    _playerSessionData.isCreated = true;
                    _playerSessionData.cups = 0;
                    _playerSessionData.language = "es";
                    _playerSessionData.isMute = false;
                    _playerSessionData.volume = 1;

                    Debug.Log($"[Auth] New anonymous session created: {_playerSessionData.playerName}");
                }

                // ✅ Sync with backend
                var loginUseCase = ServiceLocator.Instance.GetService<ILoginUseCase>();
                try
                {
                    await loginUseCase.AddPlayer(_playerSessionData,
                        "dev_player_key_change_in_production");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Auth] AddPlayer failed, continuing with LoginPlayer. {ex.Message}");
                }

                await loginUseCase.LoginPlayer(_playerSessionData.playerId);

                // ✅ Language setup
                ServiceLocator.Instance.GetService<IMultiLanguageService>()
                    .ChangeLanguage(_playerSessionData.language);

                Debug.Log("[Auth] Anonymous sign-in completed successfully.");
                return true;
            }
            catch (AuthenticationException ex)
            {
                Debug.LogError($"[Auth] AuthenticationException: {ex}");
                if (attempt >= maxRetries)
                {
                    ServiceLocator.Instance.GetService<IMessageService>()
                        .ErrorMessage("Error iniciando sesión, intenta de nuevo más tarde.").Forget();
                    return false;
                }
            }
            catch (RequestFailedException ex)
            {
                Debug.LogError($"[Auth] RequestFailedException: {ex.Message}");
                if (attempt >= maxRetries)
                {
                    ServiceLocator.Instance.GetService<IMessageService>()
                        .ErrorMessage("Problema de conexión, por favor revisa tu internet.").Forget();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Auth] Exception: {ex}");
                AuthenticationService.Instance.SignOut();

                if (attempt >= maxRetries)
                {
                    ServiceLocator.Instance.GetService<IMessageService>()
                        .ErrorMessage("Error inesperado, intenta más tarde.").Forget();
                    return false;
                }
            }

            // ⏳ Exponential backoff
            int delayMs = attempt * 2000; // 2s, 4s, 6s...
            await UniTask.Delay(delayMs);
        }

        return false;
    }


    public async UniTask SaveData(string key, object value)
    {
        var data = new Dictionary<string, object> { { key, value } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }

    public int GetExperienceToWin()
    {
        return ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig().exp_by_win;
    }

    public int GetExperienceToLose()
    {
        return ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig().exp_by_lose;
    }

    public List<PowerUpData> GetListOfPowerUps()
    {
        return _powerUpsConfigurationInstance.GetPowerUps()
            .Select(powerUp => new PowerUpData { id = powerUp.Value.Id, selected = false }).ToList();
    }

    public int HowMuchPowerUpsByBuy(string name)
    {
        var upper = name.ToUpper();

        return upper switch
        {
            var s when s.Contains("LEAD") => ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .powerup_lead_per_buy,
            var s when s.Contains("RAY") => ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .powerup_ray_per_buy,
            var s when s.Contains("ICE") => ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .powerup_ice_per_buy,
            var s when s.Contains("JOKER") => ServiceLocator.Instance.GetService<IGameConfigurationUseCase>()
                .GetConfig().powerup_joker_per_buy,
            _ => 0
        };
    }
}

public interface IGlobalSessionService
{
    int GetExperienceToWin();
    int GetExperienceToLose();
    List<PowerUpData> GetListOfPowerUps();

    int HowMuchPowerUpsByBuy(string name);
}

/// <summary>
/// To Save and Load data from the storage
/// Use this interface to save and load data from the storage
/// To global data use SaveDataGlobal and LoadDataGlobal use GlobalData enum
/// And create a new enum for each global data; please dont use string
/// </summary>
public interface IStorageData
{
    UniTask SaveData(string key, object value);
    UniTask UpdatePlayerData(PlayerSessionDataCustom player);
    void SaveDataGlobal(GlobalData key, string value);

    string LoadDataGlobal(GlobalData key);
}

public enum GlobalData
{
    GameMode
}

public enum GameMode
{
    SinglePlayer,
    MultiPlayer,
    WithFriends
}

public interface IPlayerSessionService
{
    UniTask Initialize();
    UniTask Login();
    UniTask Logout();
    PlayerSessionDataCustom GetPlayerSessionData();
    UniTask AddCoins(int coins);
    UniTask AddMatchGold(int amount);
    bool PowerUpSelected(string powerUpName, bool isSelected);
    void AddPowerUpToPlayer(StoreItemDataFromCloud powerUp, int count);
    bool CheckPowerUp(string powerUpId);
}


[Serializable]
public class PowerUpData
{
    public string id;
    public bool selected;
    public int quantity;
}

[Serializable]
public class PowerUpsData
{
    public List<PowerUpData> powerups;
}