using System.Collections.Generic;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour, IMediatorBalance, IMediatorBallZone, IPlayer, IPlayerReference
{
    [SerializeField] private Balance balance;
    [SerializeField] private BallZone ballZone;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private int countOfPointToGetPowerUp;
    [SerializeField] private string id;
    [SerializeField] private FreezePlayerMono freezePlayerMono;
    private int _coins;
    private bool _readyToPlay;
    private readonly CommandQueue _commandQueue = new();


    public string Id => id;

    public void AddCommand(ICommand command)
    {
        _commandQueue.AddCommand(command);
    }

    public bool IsBusy()
    {
        return _commandQueue.IsBusy();
    }

    public void ClearCommands()
    {
        _commandQueue.ClearCommands();
    }

    public void EnableControls()
    {
        balance.EnableControls();
        ballZone.EnableControls();
    }

    public void ConsumePowerUp(string prefabID)
    {
        ServiceLocator.Instance.GetService<IPowerUpsUseCase>().ConsumePowerUp(prefabID).Forget();

        ServiceLocator.Instance.GetService<IPowerUpsUseCase>().LoadPowerUps().Forget();
    }

    public IReadOnlyList<PowerUpData> GetPowerUps()
    {
        return ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps();
    }

    private bool _isGameOver;
    private IGameManager _gameManager;
    private int _countOfPoints = 0;

    public void Configure(IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps)
    {
        balance.OnBalanceClicked += OnBalanceClicked;
        balance.OnGetPoints += OnGetPoints;

        _gameManager = gameManager;


        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new OpeningGame(_gameManager.GetDirectorOpening()), this);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new PlayMusic(_gameManager.GetMusic()), this);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new ConfigureBallZone(this, powerUps), this);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new PlayerReadyToPlay(this), this);
        // _readyToPlay = true;
        //Debug.Log("Player Configure");
    }

    public bool IsReadyToPlay()
    {
        return _readyToPlay;
    }

    public void ConfigureBallZone(IReadOnlyList<PowerUpData> powerUps)
    {
        balance.Configure(this);
        ballZone.Configure(this, _gameManager, powerUps);
    }

    private void OnGetPoints()
    {
        _countOfPoints++;
        if (
            _countOfPoints < ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .how_much_match_to_get_power_up
            || !ballZone.CanCreatePowerUp()
            || ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetPowerUps().Count <= 0
        ) return;
        //TODO validar aqui que pueda crear powerups o que tenga para crear
        _countOfPoints = 0;
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("FeedPowerUp");
        ballZone.CreatePowerUp();
    }

    private void OnBalanceClicked(PathType sidePathType)
    {
        var result = ServiceLocator.Instance.GetService<ICommandQueue>().IsBusy(this);
        if (result) return;
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new TryBallInBalanceCommand(ballZone, sidePathType, balance), this);
    }

    public void AddPoints()
    {
        _coins++;
        scoreText.text = $"{_coins}";
    }

    public void GameOver()
    {
        _isGameOver = true;
    }

    public IPlayer GetPlayer()
    {
        return this;
    }

    public bool IsGameOver()
    {
        return _isGameOver;
    }

    public void DisableControls()
    {
        balance.DisableControls();
        ballZone.DisableControls();
    }

    public IBalance GetBalance()
    {
        return balance;
    }

    public IBallZone GetBallZone()
    {
        return ballZone;
    }

    public IGameManager GetGameManager()
    {
        return _gameManager;
    }

    public void FreezeGame(bool isFreeze)
    {
        if (isFreeze)
        {
            freezePlayerMono.Freeze();
        }
        else
        {
            freezePlayerMono.Unfreeze();
        }
    }

    private void OnDestroy()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(this);
    }

    public int GetCoins()
    {
        return _coins;
    }

    public void ReadyToPlay()
    {
        _readyToPlay = true;
    }
}