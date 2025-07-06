using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Economy;
using Unity.Services.Friends;
using UnityEngine;

public class PlayerSession : MonoBehaviour, IPlayerSessionService, IStorageData, IGlobalSessionService
{
    [SerializeField] private string environmentName;
    [SerializeField] private int winExperience, loseExperience;
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    private PlayerSessionDataCustom _playerSessionData;
    private bool _readyToSignIn;
    private PowerUpsConfiguration _powerUpsConfigurationInstance;

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
}

public interface IGlobalSessionService
{
    int GetExperienceToWin();
    int GetExperienceToLose();
    List<PowerUpData> GetListOfPowerUps();
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
public class PlayerSessionDataCustom
{
    public string playerId;
    public string playerName;
    public string playerEmail;
    public string playerPhotoUrl;
    public bool isCreated;
    public int cups;
    public string language;
    public bool isMute;
    public float volume;
    public string powerups_json;

    public List<PowerUpData> GetPowerUpsDummy()
    {
        // ListOfPowerUps
        return new List<PowerUpData>
        {
            new() { id = "lead", selected = false },
            new() { id = "ray", selected = false },
            new() { id = "joker", selected = false },
            new() { id = "ice", selected = false },
        };
    }

    public Dictionary<string, object> GetData()
    {
        return new Dictionary<string, object>
        {
            { "playerId", playerId },
            { "playerName", playerName },
            { "playerEmail", playerEmail },
            { "playerPhotoUrl", playerPhotoUrl },
            { "isCreated", isCreated },
            { "cups", cups },
            { "language", language },
            { "isMute", isMute },
            { "volume", volume },
            { "powerups_json", powerups_json }
        };
    }

    public async UniTask<int> GetCoins()
    {
        var options = new GetBalancesOptions
        {
            ItemsPerFetch = 5
        };

        var getBalancesResult = await EconomyService.Instance.PlayerBalances.GetBalancesAsync(options);
        var firstFiveBalances = getBalancesResult.Balances;
        // do something with your balances
        foreach (var balance in firstFiveBalances)
        {
            //Debug.Log($"CurrencyId: {balance.CurrencyId}");
            if (balance.CurrencyId == "COINS")
            {
                return (int)balance.Balance;
            }
        }

        throw new Exception("Coins not found");
    }

    public void LoadData(Dictionary<string, Item> loadAllAsync)
    {
        foreach (var result in loadAllAsync)
        {
            switch (result.Key)
            {
                case "playerId":
                    playerId = result.Value.Value.GetAsString();
                    break;
                case "playerName":
                    playerName = result.Value.Value.GetAsString();
                    break;
                case "playerEmail":
                    playerEmail = result.Value.Value.GetAsString();
                    break;
                case "playerPhotoUrl":
                    playerPhotoUrl = result.Value.Value.GetAsString();
                    break;
                case "isCreated":
                    isCreated = result.Value.Value.GetAs<bool>();
                    break;
                case "cups":
                    cups = result.Value.Value.GetAs<int>();
                    break;
                case "language":
                    language = result.Value.Value.GetAsString();
                    break;
                case "isMute":
                    isMute = result.Value.Value.GetAs<bool>();
                    break;
                case "volume":
                    volume = result.Value.Value.GetAs<float>();
                    break;
                case "powerups_json":
                    powerups_json = result.Value.Value.GetAsString();
                    break;
            }
        }
    }

    public List<PowerUpData> GetPowerUps()
    {
        try
        {
            Debug.Log(powerups_json);
            return JsonUtility.FromJson<PowerUpsData>(powerups_json).powerups;
        }
        catch (Exception e)
        {
            return new List<PowerUpData>();
        }
    }

    public async UniTask<RewardDataJson> AddReward(RewardData rewardData)
    {
        var rewardDataC = JsonUtility.FromJson<RewardDataJson>(rewardData.reward);

        Debug.Log(rewardDataC.type);
        Debug.Log(rewardDataC.count);

        switch (rewardDataC.type)
        {
            case "gold":
                await ServiceLocator.Instance.GetService<IPlayerSessionService>()
                    .AddCoins(int.Parse(rewardDataC.count));
                break;
            case "powerup":
                AddPowerUpToPlayer(rewardDataC.count);
                break;
            default:
                throw new Exception($"Reward type {rewardDataC.type} not supported yet");
        }

        return rewardDataC;
    }

    public void AddPowerUpToPlayer(string rewardDataC)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = ServiceLocator.Instance.GetService<IGlobalSessionService>().GetListOfPowerUps();
        var powerUpData = powerUpsData.Find(x => x.id == rewardDataC);
        var allPowerUps = player.GetPowerUps();

        if (allPowerUps.Any(x => x.id == rewardDataC))
        {
            throw new Exception($"El powerup {rewardDataC} ya esta en posesion del jugador");
        }

        allPowerUps.Add(powerUpData);
        player.powerups_json = JsonUtility.ToJson(new PowerUpsData { powerups = allPowerUps });
        ServiceLocator.Instance.GetService<IStorageData>().UpdatePlayerData(player).Forget();
    }

    public bool CheckPowerUp(string powerUpId)
    {
        var player = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData();
        var powerUpsData = ServiceLocator.Instance.GetService<IGlobalSessionService>().GetListOfPowerUps();
        var powerUpData = powerUpsData.Find(x => x.id == powerUpId);
        var allPowerUps = player.GetPowerUps();

        return allPowerUps.Any(x => x.id == powerUpData.id);
    }
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