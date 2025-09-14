using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Economy;
using Unity.Services.Friends;
using Unity.Services.RemoteConfig;
using UnityEngine;

public class PlayerSession : MonoBehaviour, IPlayerSessionService, IStorageData, IGlobalSessionService
{
    [SerializeField] private string environmentName;
    private int winExperience, loseExperience;
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    private PlayerSessionDataCustom _playerSessionData;
    private bool _readyToSignIn;
    private PowerUpsConfiguration _powerUpsConfigurationInstance;


    private int powerUpIcePerBuy;
    private int powerUpRayPerBuy;
    private int powerUpLeadPerBuy;
    private int powerUpJokerPerBuy;

    public struct userAttributes
    {
    }

    public struct appAttributes
    {
    }

    private async void Awake()
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
            await Initialize();
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

        _playerSessionData = new PlayerSessionDataCustom();
    }

    public async UniTask Login()
    {
        if (!_readyToSignIn)
        {
            ServiceLocator.Instance.GetService<IMessageService>().ErrorMessage("PlayerSession is not ready to sign in");
            return;
        }

        await SignInAnonymouslyAsync();
        await UpdatePlayerData(_playerSessionData);

        RemoteConfigService.Instance.FetchCompleted += ApplyRemoteSettings;
        RemoteConfigService.Instance.FetchConfigs(new userAttributes(), new appAttributes());
    }

    private void ApplyRemoteSettings(ConfigResponse obj)
    {
        winExperience = RemoteConfigService.Instance.appConfig.GetInt("exp_by_win");
        loseExperience = RemoteConfigService.Instance.appConfig.GetInt("exp_by_lose");
        powerUpIcePerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_ice_per_buy");
        powerUpJokerPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_joker_per_buy");
        powerUpLeadPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_lead_per_buy");
        powerUpRayPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_ray_per_buy");
        Debug.Log($"powerUpRayPerBuy {powerUpRayPerBuy} powerUpLeadPerBuy {powerUpLeadPerBuy}");
    }

    public async UniTask UpdatePlayerData(PlayerSessionDataCustom player)
    {
        foreach (var (key, value) in player.GetData())
        {
            await SaveData(key, value);
        }

        _playerSessionData = player;
        await ServiceLocator.Instance.GetService<IHttpRequestsCustom>()
            .UpdatePlayerNickname(_playerSessionData.playerId, _playerSessionData.playerName);
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
        await EconomyService.Instance.PlayerBalances.IncrementBalanceAsync("COINS", coins);
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

    public void AddPowerUpToPlayer(string powerUpId)
    {
        _playerSessionData.AddPowerUpToPlayer(powerUpId);
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(_playerSessionData).Forget();
    }

    public bool CheckPowerUp(string powerUpId)
    {
        return _playerSessionData.CheckPowerUp(powerUpId);
    }

    private async UniTask<bool> HasSavedSessionData()
    {
        try
        {
            var keys = await CloudSaveService.Instance.Data.Player.ListAllKeysAsync();
            if (keys.Any(itemKey => itemKey.Key == "isCreated"))
            {
                var datas = await CloudSaveService.Instance.Data.Player.LoadAllAsync();
                foreach (var data in datas.Where(data => data.Key == "isCreated"))
                {
                    _playerSessionData.isCreated = bool.TryParse(data.Value.ToString(), out var isCreated) && isCreated;
                    return true;
                }
            }

            throw new Exception("Data not found");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
    }

    async UniTask SignInAnonymouslyAsync()
    {
        try
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
            }

            //Debug.Log($"AuthenticationService.Instance.IsSignedIn {AuthenticationService.Instance.IsSignedIn}");
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            //Debug.Log($"AuthenticationService.Instance.IsSignedIn {AuthenticationService.Instance.IsSignedIn}");
            // Initialize friends service
            await FriendsService.Instance.InitializeAsync();
            // Start using the Friends SDK functionalities.
            var friends = FriendsService.Instance.Friends;
            foreach (var friend in friends)
            {
                //Debug.Log($"Friend: {friend}");
            }

            if (await HasSavedSessionData())
            {
                //Debug.Log("Sign in anonymously skipped!");
                var a = await CloudSaveService.Instance.Data.Player.LoadAllAsync();

                _playerSessionData.LoadData(a);

                //Debug.Log($"Player ID: {_playerSessionData.playerName}");
            }
            else
            {
                _playerSessionData.playerId = AuthenticationService.Instance.PlayerId;
                _playerSessionData.playerName = SystemInfo.deviceUniqueIdentifier;
                _playerSessionData.playerEmail = "Anonymous";
                _playerSessionData.playerPhotoUrl = "Anonymous";
                _playerSessionData.isCreated = true;
                _playerSessionData.cups = 0;
                _playerSessionData.language = "es";
                _playerSessionData.isMute = false;
                _playerSessionData.volume = 1;
                //Debug.Log("Sign in anonymously succeeded!");
                // Guardar el nickname con el playerId actual
                var data = new Dictionary<string, object>
                {
                    { _playerSessionData.playerName, AuthenticationService.Instance.PlayerId }
                };
                //await CloudSaveService.Instance.Data.Custom.
                //Debug.Log($"Nickname '{_playerSessionData.playerName}' has been saved with player ID.");
            }

            await ServiceLocator.Instance.GetService<IHttpRequestsCustom>().AddPlayer(_playerSessionData.playerId,
                _playerSessionData.playerName, "your_player_key");

            await ServiceLocator.Instance.GetService<IHttpRequestsCustom>()
                .LoginPlayer(_playerSessionData.playerId);

            ServiceLocator.Instance.GetService<IMultiLanguageService>().ChangeLanguage(_playerSessionData.language);
            await ServiceLocator.Instance.GetService<IHttpRequestsCustom>()
                .UpdatePlayerNickname(_playerSessionData.playerId, _playerSessionData.playerName);
        }
        catch (AuthenticationException ex)
        {
            // Compare error code to AuthenticationErrorCodes
            // Notify the player with the proper error message
            Debug.Log(ex);
            throw new Exception("Ocurrio un error iniciando sesion intenta de nuevo AuthenticationException");
        }
        catch (RequestFailedException ex)
        {
            // Compare error code to CommonErrorCodes
            // Notify the player with the proper error message
            Debug.LogError($"Error RequestFailedException: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error Exception: {ex.Message}");
            AuthenticationService.Instance.SignOut();
            throw;
        }
    }

    public async UniTask SaveData(string key, object value)
    {
        var data = new Dictionary<string, object> { { key, value } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
    }

    public int GetExperienceToWin()
    {
        return winExperience;
    }

    public int GetExperienceToLose()
    {
        return loseExperience;
    }

    public List<PowerUpData> GetListOfPowerUps()
    {
        return _powerUpsConfigurationInstance.GetPowerUps()
            .Select(powerUp => new PowerUpData { id = powerUp.Value.Id, selected = false }).ToList();
    }

    public int howMuchPowerUpsByBuy(string name)
    {
        Debug.Log($"name {name}");
        return name switch
        {
            "LEAD" => powerUpLeadPerBuy,
            "RAY" => powerUpRayPerBuy,
            "ICE" => powerUpIcePerBuy,
            "JOKER" => powerUpJokerPerBuy,
            _ => 0
        };
    }
}
public interface IGlobalSessionService
{
    int GetExperienceToWin();
    int GetExperienceToLose();
    List<PowerUpData> GetListOfPowerUps();

    int howMuchPowerUpsByBuy(string name);
}

public interface IHttpRequestsCustom
{
    UniTask AddPlayer(string playerId, string nickname, string key);
    UniTask UpdatePlayerNickname(string playerId, string playerName);
    UniTask<string> LoginPlayer(string playerId);

    UniTask<PlayerData> GetPlayerById(string playerId);
    PlayerRewardApi PlayerRewardApi { get; }
    PlayerApi PlayerApi { get; }
    BattlePassApi BattlePassApi { get; }
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
    bool PowerUpSelected(string powerUpName, bool isSelected);
    void AddPowerUpToPlayer(string powerUpId);
    bool CheckPowerUp(string powerUpId);
}


[Serializable]
public class PowerUpData
{
    public string id;
    public bool selected;
}

[Serializable]
public class PowerUpsData
{
    public List<PowerUpData> powerups;
}

[Serializable]
public class PlayerBattlePass
{
    public int id;
    public string playerId;
    public int level;
    public int experience;
    public DateTime created_at;
    public DateTime updated_at;
    public List<PlayerReward> playerRewards;
}

[Serializable]
public class PlayerReward
{
    public int id;
    public string playerId;
    public int rewardId;
    public DateTime awarded_at;
    public bool claimed;
}