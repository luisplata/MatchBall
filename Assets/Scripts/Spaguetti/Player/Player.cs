using System.Collections.Generic;
using App.UseCase.GameConfiguration;
using App.UseCase.PowerUps;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using Spaguetti.Game;
using TMPro;
using UnityEngine;

public class Player : MonoBehaviour, IMediatorBalance, IMediatorBallZone, IPlayer, IPlayerReference
{
    [SerializeField] private Balance balance;
    [SerializeField] private BallZone ballZone;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI nameText;
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
        // A FreezeBall in flight must not revive a board that already won or lost.
        if (_isGameOver) return;
        balance.EnableControls();
        ballZone.EnableControls();
    }

    public void ConsumePowerUp(string prefabID)
    {
        // In local PvP the orb is paid from this player's match budget: memory only. The use case is
        // the only writer of the account inventory, so returning here is the whole no-persistence
        // guarantee for power-ups.
        if (_budget != null)
        {
            _budget.Consume(prefabID);
            return;
        }

        ConsumePowerUpAsync(prefabID).Forget();
    }

    private async UniTaskVoid ConsumePowerUpAsync(string prefabID)
    {
        try
        {
            // PowerUpsUseCase.ConsumePowerUp already reloads the inventory; do not reload twice.
            await ServiceLocator.Instance.GetService<IPowerUpsUseCase>().ConsumePowerUp(prefabID);
        }
        catch (System.Exception exception)
        {
            // The effect was already committed at click time; the write is best-effort and must stay visible.
            Debug.LogError($"[PowerUp] consume '{prefabID}' failed: {exception}");
        }
    }

    /// <summary>
    /// Selected power-ups projected onto this match's remaining budget: owned (persisted) minus
    /// the orbs of that type already on the board. Types with no budget are dropped, so the random
    /// spawn pool is filtered per type: an aggregate count would let an exhausted type spawn a
    /// budgetless orb.
    ///
    /// In local PvP the owned quantity comes from this player's private match budget instead of the
    /// account inventory; the reservation half of the formula is unchanged and stays local to the
    /// board that asked.
    /// </summary>
    public IReadOnlyList<PowerUpData> GetPowerUps()
    {
        if (_budget != null && ballZone != null)
            return _budget.Available(ballZone.CountReservedPowerUps);

        var selectedPowerUps = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps();
        if (ballZone == null) return selectedPowerUps;

        var budget = new List<PowerUpData>(selectedPowerUps.Count);
        foreach (var powerUp in selectedPowerUps)
        {
            var available = powerUp.quantity - ballZone.CountReservedPowerUps(powerUp.id);
            if (available <= 0) continue;
            budget.Add(new PowerUpData
            {
                id = powerUp.id,
                selected = powerUp.selected,
                quantity = available
            });
        }

        return budget;
    }

    private bool _isGameOver;
    private IGameManager _gameManager;
    private int _countOfPoints = 0;

    // Non-null iff this match is local PvP. That equivalence is the discriminator every economy
    // path reads: null means "behave exactly as before this change".
    private MatchPowerUpBudget _budget;

    public void Configure(IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps)
    {
        RefreshHud();

        balance.OnBalanceClicked -= OnBalanceClicked;
        balance.OnBalanceClicked += OnBalanceClicked;
        balance.OnGetPoints -= OnGetPoints;
        balance.OnGetPoints += OnGetPoints;

        _gameManager = gameManager;
        _budget = gameManager != null && gameManager.IsLocalPvP()
            ? new MatchPowerUpBudget(powerUps, $"P1/{Id}")
            : null;
        if (_budget != null)
            Debug.Log($"[PvP] P1 budget for match: {_budget.Describe()}.");


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
        // Tutorial matches never award power-ups. The orb factory reads the live inventory
        // (PowerUpsFactory.Create calls _player.GetPowerUps()), so the empty budget handed to
        // Configure is not enough to keep one off the lesson board — this is the award choke point.
        if (_gameManager != null && _gameManager.IsTutorialMode()) return;

        _countOfPoints++;
        if (
            _countOfPoints < ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .how_much_match_to_get_power_up
            || !ballZone.CanCreatePowerUp()
            || GetPowerUps().Count <= 0
        ) return;
        //TODO validar aqui que pueda crear powerups o que tenga para crear
        _countOfPoints = 0;
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("FeedPowerUp");
        ballZone.CreatePowerUp();
    }

    private void OnBalanceClicked(PathType sidePathType)
    {
        // The editor A/D shortcut raises this on BOTH boards; this gate is what keeps it from
        // driving the inactive one (and from passing the turn twice). True outside PvP.
        if (ballZone != null && !ballZone.IsInputEnabled()) return;
        var result = ServiceLocator.Instance.GetService<ICommandQueue>().IsBusy(this);
        if (result) return;
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new TryBallInBalanceCommand(ballZone, sidePathType, balance), this);
        _gameManager?.NotifyPlayerActed(this);
    }

    public void AddPoints(int points)
    {
        _coins += points;
        RefreshHud();
    }

    private void RefreshHud()
    {
        if (scoreText != null) scoreText.text = _coins.ToString();
        else Debug.LogWarning("[Player] scoreText is not wired.");

        if (nameText != null) nameText.text = "Player";
        else Debug.LogWarning("[Player] nameText is not wired.");
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
        balance.OnBalanceClicked -= OnBalanceClicked;
        balance.OnGetPoints -= OnGetPoints;

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