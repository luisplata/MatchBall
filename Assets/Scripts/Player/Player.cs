using Commander;
using Commander.Commands;
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

    private bool _isGameOver;
    private IGameManager _gameManager;
    private int _countOfPoints = 0;

    public void Configure(IGameManager gameManager)
    {
        balance.OnBalanceClicked += OnBalanceClicked;
        balance.OnGetPoints += OnGetPoints;

        _gameManager = gameManager;
        
        
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new OpeningGame(_gameManager.GetDirectorOpening()), this);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new PlayMusic(_gameManager.GetMusic()), this);
        ServiceLocator.Instance.GetService<ICommandQueue>().AddCommand(new ConfigureBallZone(this), this);
        //Debug.Log("Player Configure");
        _readyToPlay = true;
    }

    public bool ReadyToPlay()
    {
        return _readyToPlay;
    }

    public void ConfigureBallZone()
    {
        balance.Configure(this);
        ballZone.Configure(this, _gameManager);
    }

    private void OnGetPoints()
    {
        _countOfPoints++;
        if (_countOfPoints < countOfPointToGetPowerUp || !ballZone.CanCreatePowerUp()) return;
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
}

public interface IPlayerReference
{
    IPlayer GetPlayer();
    void Configure(IGameManager gameManager);
    bool ReadyToPlay();
}

public interface IPlayer
{
    string Id { get; }
    void AddCommand(ICommand command);
    bool IsBusy();
    void Configure(IGameManager gameManager);
    int GetCoins();
    bool IsGameOver();
    void DisableControls();
    void ConfigureBallZone();
    IBalance GetBalance();
    IBallZone GetBallZone();
    IGameManager GetGameManager();
    void FreezeGame(bool isFreeze);
    void ClearCommands();
    void EnableControls();
}

public interface IMediatorBalance
{
    void AddPoints();
    void GameOver();
    IPlayer GetPlayer();
}

public interface IMediatorBallZone
{
    IBalance GetBalance();
    IPlayer GetPlayer();
}