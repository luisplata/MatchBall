using System.Collections.Generic;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Commander;
using Cysharp.Threading.Tasks;
using Spaguetti.Game;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class GameManager : MonoBehaviour, IGameManager, IGameWinConditionContext
{
    [SerializeField, InterfaceType(typeof(IPlayerReference))]
    private Object player1;

    private IPlayerReference PlayerRef1 => player1 as IPlayerReference;

// Optionally, adding a private property is recommended for
// automatically casting the object field to the interface.
    private IPlayer Player1 => PlayerRef1.GetPlayer();

    [SerializeField] private OtherPlayer player2;
    [SerializeField] private GameObject gameOverPanel, youWinPanel;
    [SerializeField] private GameObject messagePanel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private string music;
    [SerializeField] private PlayableDirector director;
    private bool _isGameOver;
    private int _multiplier = 1;

    private readonly FullBalanceCondition _fullBalanceCondition = new();
    private readonly FirstTo100PointsRule _firstTo100PointsRule = new();
    private readonly TimeAttackRule _timeAttackRule = new();
    private List<IWinCondition> _winConditions = new();
    private IWinCondition _winCondition;
    private float _elapsedGameTime;
    [SerializeField] private TextMeshProUGUI gameMode;

    private void Awake()
    {
        //hide result panels before any fallible boot code runs
        gameOverPanel.SetActive(false);
        youWinPanel.SetActive(false);
        HideMessage();

        //set FPS to 60
        Application.targetFrameRate = 60;
        //lock to portrait mode
        Screen.orientation = ScreenOrientation.Portrait;
        //set screen dont sleep
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        //set dont restart app if it was paused
        Application.runInBackground = true;
        _winConditions.Add(_firstTo100PointsRule);
        _winConditions.Add(_timeAttackRule);
    }

    public void DuplcateCoins()
    {
        ServiceLocator.Instance.GetService<IAdModUseCase>().ShowRewarded(d => { _multiplier = (int)d; });
    }

    private void Start()
    {
        ServiceLocator.Instance.GetService<IMusicController>().StopMusic();

        _isGameOver = false;

        //load win condition from random wincondition and inject context
        _winCondition = _winConditions[Random.Range(0, _winConditions.Count)];
        _winCondition.SetContext(this);
        gameMode.text = $"Game Mode: {_winCondition.GetDescription()}";
        Debug.Log($"Selected win condition: {_winCondition.GetDescription()}");

        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player1);
        var randomToUsePowerUp = ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
            .random_to_use_power_up;
        player2.AddPlayerInQueue(this, randomToUsePowerUp);

        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
        {
            PlayerRef1.Configure(this, ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps());

            //get the gamemode to configure the game
            var gameMode = ServiceLocator.Instance.GetService<IStorageData>().LoadDataGlobal(GlobalData.GameMode);
            if (gameMode == GameMode.SinglePlayer.ToString())
            {
            }
            else if (gameMode == GameMode.MultiPlayer.ToString())
            {
            }
            else if (gameMode == GameMode.WithFriends.ToString())
            {
            }
            else
            {
                //player2.Configure(this, (float)director.duration);
            }

            player2.Configure(this, (float)director.duration, ServiceLocator.Instance
                .GetService<IPlayerSessionService>().GetPlayerSessionData()
                .GetPowerUpsDummy());
        });
    }

    private void Update()
    {
        if (!ArePlayersReady() || _isGameOver) return;

        // Track elapsed game time for time-based win conditions
        _elapsedGameTime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (!PlayerRef1.IsReadyToPlay() || !player2.ReadyToPlay()) return;

        // Update win condition (e.g., accumulate timer)
        _winCondition.OnFrameUpdate();

        if (_isGameOver) return;

        // Check full balance first (always has priority - safety fallback)
        WinResult balanceResult = _fullBalanceCondition.CheckWinCondition(Player1, player2.GetPlayer());
        if (balanceResult.IsValid)
        {
            ResolveGameOver(balanceResult);
            return;
        }

        // Check active win condition
        WinResult conditionResult = _winCondition.CheckWinCondition(Player1, player2.GetPlayer());
        if (conditionResult.IsValid)
        {
            ResolveGameOver(conditionResult);
        }
    }

    private void ResolveGameOver(WinResult result)
    {
        Player1.DisableControls();
        _isGameOver = true;
        ServiceLocator.Instance.GetService<IMusicController>().StopMusic();
        Debug.Log($"Game Over! Winner: {result.Winner.GetType().Name}, Loser: {result.Loser.GetType().Name}");

        bool playerWon = result.Winner == Player1;

        if (playerWon)
        {
            youWinPanel.SetActive(true);
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Win");
            ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(true).Forget();
        }
        else
        {
            gameOverPanel.SetActive(true);
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Lose");
            ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(false).Forget();
        }
    }

    #region IGameWinConditionContext Implementation

    public float GetElapsedGameTime()
    {
        return _elapsedGameTime;
    }

    public bool ArePlayersReady()
    {
        return PlayerRef1.IsReadyToPlay() && player2.ReadyToPlay();
    }

    #endregion

    private void OnDestroy()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>().ClearCommands(Player1);
        ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(Player1);
        ServiceLocator.Instance.GetService<ICommandQueue>().ClearCommands(player2.GetPlayer());
        ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(player2.GetPlayer());
    }

    public void RestartApplication()
    {
        ServiceLocator.Instance.GetService<ITransitionService>().ShowSplashScreen(AddCoinsAndLoadScene);
    }

    private async void AddCoinsAndLoadScene()
    {
        await ServiceLocator.Instance.GetService<IPlayerSessionService>().AddMatchGold(Player1.GetCoins() * _multiplier);
        SceneManager.LoadScene("BallPass");
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
        messagePanel.SetActive(true);
    }

    public void HideMessage()
    {
        messagePanel.SetActive(false);
        messageText.text = string.Empty;
    }

    public PlayableDirector GetDirectorOpening()
    {
        return director;
    }

    public string GetMusic()
    {
        return music;
    }

    public IPlayer GetFirstPlayer()
    {
        return Player1;
    }

    public bool IsGameOver()
    {
        return _isGameOver;
    }

    public IPlayer GetOtherPlayer(IPlayer player)
    {
        return player is MachinePlayer ? Player1 : player2.GetPlayer();
    }
}

public interface IGameManager
{
    void ShowMessage(string message);
    void HideMessage();
    PlayableDirector GetDirectorOpening();
    string GetMusic();
    IPlayer GetFirstPlayer();
    bool IsGameOver();
    IPlayer GetOtherPlayer(IPlayer player);
}