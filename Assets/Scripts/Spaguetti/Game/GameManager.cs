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
    private bool _isLocalPvP;

    /// <summary>Opponent HUD state captured before the tutorial hid it, so it can be handed back.</summary>
    private bool _opponentHudWasActive;
    private bool _opponentHudHidden;

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
        ConsumeLocalPvP();
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

        // The opponent's scoreboard is not a child of its board, so disabling P2 leaves it drawn in
        // the top band. There is no AI in the lesson: take it off screen and remember what it was.
        HideTutorialOpponentHud();

        var ballFactory = Player1.GetBallZone()?.GetBallFactory();
        ballFactory?.SetConsumptionOrder(TutorialBallOrder);
        Debug.Log("[Tutorial] tutorial mode active: random win condition, AI, XP, gold and power-up spend suppressed.");
    }

    /// <summary>
    /// Reads the transient local-PvP flag and consumes it immediately, so a second boot is a normal
    /// match. In PvP it also flips the second board so it reads upright from the opposite seat; in
    /// PvE and the tutorial nothing here runs and the board keeps its authored transform.
    /// </summary>
    private void ConsumeLocalPvP()
    {
        var storage = ServiceLocator.Instance.GetService<IStorageData>();
        _isLocalPvP = storage.LoadDataGlobal(GlobalData.LocalPvP) == "1";
        storage.SaveDataGlobal(GlobalData.LocalPvP, string.Empty);

        // A stale flag left by an interrupted PvP route must never take over the lesson: the
        // tutorial has one player and would end up with a flipped board for nobody.
        if (_isTutorialMode) _isLocalPvP = false;

        if (!_isLocalPvP) return;

        Debug.Log("[PvP] local PvP mode active: P2 is human, no Matching, no ad, no Lobby.");

        // P2 sits opposite P1: mirror its board about its own columns and turn it 180 degrees.
        // Runs in Awake, before the splash hides, so no unflipped frame is ever rendered.
        var boardView = player2 != null ? player2.GetComponent<PlayerBoardView>() : null;
        if (boardView == null)
        {
            Debug.LogError("[PvP] P2 has no PlayerBoardView; the board is left unflipped.");
            return;
        }

        boardView.ApplyOrientation(true);
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
        player2.AddPlayerInQueue(this, randomToUsePowerUp, _isLocalPvP);

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

            player2.Configure(this, (float)director.duration,
                _isLocalPvP
                    // A guest inherits the host's selection exactly; never the zero-quantity dummy.
                    ? ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps()
                    : ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData()
                        .GetPowerUpsDummy(),
                _isLocalPvP ? "Player 2" : "AI");
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
        if (_isLocalPvP) player2.GetPlayer()?.DisableControls();
        _isGameOver = true;
        ServiceLocator.Instance.GetService<IMusicController>().StopMusic();
        Debug.Log($"Game Over! Winner: {result.Winner.GetType().Name}, Loser: {result.Loser.GetType().Name}");

        // The panel stays P1-framed on purpose (a P2 win shows the game-over panel); what matters
        // for local PvP is that a result resolves in both directions and no account is credited.
        bool playerWon = result.Winner == Player1;

        if (playerWon)
        {
            youWinPanel.SetActive(true);
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Win");
            if (!_isLocalPvP) ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(true).Forget();
        }
        else
        {
            gameOverPanel.SetActive(true);
            ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Lose");
            if (!_isLocalPvP) ServiceLocator.Instance.GetService<ILoginUseCase>().AddExperience(false).Forget();
        }

        if (_isLocalPvP)
        {
            // Both boards are dead for either outcome. GameOver also makes a queued FreezeBall's
            // EnableControls a no-op, so a straggler command cannot revive a finished board.
            Player1.GameOver();
            player2.GetPlayer()?.GameOver();
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
        // Local PvP leaves the wallet exactly as it was: the match scores are not currency.
        if (!_isLocalPvP)
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

    /// <summary>
    /// The opponent board's root, so the tutorial can anchor its bubble to the empty half of the
    /// screen. Null-safe: the tutorial falls back to a fixed world point when it is missing.
    /// </summary>
    public Transform GetOpponentBoardRoot()
    {
        return player2 != null ? player2.transform : null;
    }

    /// <summary>
    /// Takes the opponent's HUD cluster off screen for the tutorial and records its previous state,
    /// so a cluster another system had already hidden is not switched back on by us.
    /// </summary>
    private void HideTutorialOpponentHud()
    {
        var cluster = GetOpponentHudCluster();
        if (cluster == null) return;

        _opponentHudWasActive = cluster.gameObject.activeSelf;
        _opponentHudHidden = true;
        cluster.gameObject.SetActive(false);
    }

    /// <summary>
    /// Hands the opponent HUD back to whatever state it had before the tutorial started. Called on
    /// every way out of the lesson, including the aborts, so no match inherits a hidden scoreboard.
    /// </summary>
    public void RestoreTutorialOpponentHud()
    {
        if (!_opponentHudHidden) return;

        _opponentHudHidden = false;

        var cluster = GetOpponentHudCluster();
        if (cluster == null) return;

        cluster.gameObject.SetActive(_opponentHudWasActive);
    }

    private RectTransform GetOpponentHudCluster()
    {
        if (player2 == null) return null;

        var boardView = player2.GetComponent<PlayerBoardView>();
        return boardView != null ? boardView.HudCluster : null;
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

    /// <summary>True only while this match was booted by the transient local-PvP flag.</summary>
    public bool IsLocalPvP()
    {
        return _isLocalPvP;
    }

    public IPlayer GetOtherPlayer(IPlayer player)
    {
        // Identity, not type: a human P2 is not a MachinePlayer, and the type test answered with P2
        // itself, so P2's orbs hit P2's own board. In PvE both directions map exactly as before
        // (Player -> P2, MachinePlayer -> Player1).
        return ReferenceEquals(player, Player1) ? player2.GetPlayer() : Player1;
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
    bool IsLocalPvP();
    IPlayer GetOtherPlayer(IPlayer player);
}