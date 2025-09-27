using System;
using Cysharp.Threading.Tasks;
using Infra.WebAdapter;
using UnityEngine;

public class ApiClient : MonoBehaviour, IHttpMediator
{
    [SerializeField] private string serverUrl;
    [SerializeField] private string uri;

    public PlayerApi PlayerApi { get; private set; }
    public BattlePassApi BattlePassApi { get; private set; }
    public PlayerRewardApi PlayerRewardApi { get; private set; }

    private void Awake()
    {
        if (FindObjectsOfType<ApiClient>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        ServiceLocator.Instance.RegisterService(this);

        PlayerApi = new PlayerApi(serverUrl, uri, this);
        BattlePassApi = new BattlePassApi(serverUrl, uri, this);
        PlayerRewardApi = new PlayerRewardApi(serverUrl, uri, this);
    }

    // Métodos para actualizar el token JWT en las APIs
    public void UpdateJwtToken(string token)
    {
        PlayerApi.JwtToken = token;
        BattlePassApi.JwtToken = token;
        PlayerRewardApi.JwtToken = token;
    }
}

public interface IHttpMediator
{
    void UpdateJwtToken(string token);
}

[Serializable]
public class TokenResponse
{
    public string token;
}