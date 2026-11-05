using System.Collections.Generic;
using App.UseCase;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Bootstrap;
using Commander;
using Cysharp.Threading.Tasks;
using Presentation.Tutorial;
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
    private bool _isTutorialMode;

    /// <summary>Fixed feed for the first-run lesson: drop, then build the 3+ match on tap six.</summary>
    private static readonly string[] TutorialBallOrder =
    {
        "blue", "red", "blue", "red", "blue", "blue"
    };

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

        ConsumeTutorialMode();
    }

    /// <summary>
    /// Reads the transient tutorial flag and consumes it immediately, so it can never reach a second
    /// match. In tutorial mode the AI player is switched off before its Awake/Configure chain can run
    /// and the human factory is reseeded with the lesson script.
    /// </summary>
    private void ConsumeTutorialMode()
    {
        var storage = ServiceLocator.Instance.GetService<IStorageData>();
        _isTutorialMode = storage.LoadDataGlobal(GlobalData.TutorialMode) == "1";

        if (!_isTutorialMode)
        {
            TutorialResume.Clear();
            return;
        }

        storage.SaveDataGlobal(GlobalData.TutorialMode, string.Empty);

        if (player2 != null) player2.gameObject.SetActive(false);
        if (gameMode != null) gameMode.gameObject.SetActive(false);

        var ballFactory = Player1.GetBallZone()?.GetBallFactory();
        ballFactory?.SetConsumptionOrder(TutorialBallOrder);
        Debug.Log("[Tutorial] tutorial mode active: random win condition, AI, XP, gold and power-up spend suppressed.");
    }

    public void DuplcateCoins()
    {
        ServiceLocator.Instance.GetService<IAdModUseCase>().ShowRewarded(d => { _multiplier = (int)d; });
    }

    private void Start()
    {
        ServiceLocator.Instance.GetService<IMusicController>().StopMusic();

        _isGameOver = false;

        if (_isTutorialMode)
        {
            StartTutorialMatch();
            return;
        }

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

    /// <summary>
    /// Boots the human player alone: no win condition, no AI in the command queue, and an empty
    /// power-up budget so nothing can spawn or be spent while the lesson runs.
    /// </summary>
    private void StartTutorialMatch()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>().AddPlayer(Player1);

        ServiceLocator.Instance.GetService<ITransitionService>().HideSplashScreen(() =>
        {
            PlayerRef1.Configure(this, System.Array.Empty<PowerUpData>());
            Debug.Log("[Tutorial] human player configured without power-ups; AI skipped.");
        });
    }

    private void Update()
    {
        if (_isTutorialMode) return;
        if (!ArePlayersReady() || _isGameOver) return;

        // Track elapsed game time for time-based win conditions
        _elapsedGameTime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_isTutorialMode) return;
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
        // The tutorial treats an overflow as a retry: never a real game over, never XP.
        if (_isTutorialMode) return;

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
        if (_isTutorialMode) return false;
        return PlayerRef1.IsReadyToPlay() && player2.ReadyToPlay();
    }

    #endregion

    private void OnDestroy()
    {
        // The tutorial never creates the AI player, so its commands were never queued.
        if (player2 != null && player2.GetPlayer() != null)
        {
            ServiceLocator.Instance.GetService<ICommandQueue>().ClearCommands(player2.GetPlayer());
            ServiceLocator.Instance.GetService<ICommandQueue>().RemovePlayer(player2.GetPlayer());
        }

        // The queue may already be gone while the editor tears Play mode down.
        if (!ServiceLocator.Instance.TryGetService<ICommandQueue>(out var queue) || queue == null) return;

        queue.ClearCommands(Player1);
        queue.RemovePlayer(Player1);
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

    /// <summary>True only while this match was booted by the transient tutorial flag.</summary>
    public bool IsTutorialMode()
    {
        return _isTutorialMode;
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
    bool IsTutorialMode();
    IPlayer GetOtherPlayer(IPlayer player);
}