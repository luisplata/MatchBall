using System.Collections.Generic;
using App.UseCase.GameConfiguration;
using Commander;
using Commander.Commands;
using UnityEngine;

/// <summary>
/// Second human on the local PvP board. Mirrors <see cref="MachinePlayer"/> minus the AI and minus
/// the economy: its points live only in memory, and nothing it does touches the account. The
/// match-local power-up budget lands in a later slice; for now the seed list handed by
/// <see cref="GameManager"/> is used as-is.
/// </summary>
public sealed class HumanP2Player : IPlayer, IMediatorBalance, IMediatorBallZone
{
    private readonly OtherPlayer _otherPlayer;
    private readonly FreezePlayerMono _freezePlayerMono;
    private readonly IBalance _balance;
    private readonly BallZone _ballZone;

    private readonly CommandQueue _commandQueue;

    private bool _isGameOver;
    private bool _readyToPlay;
    private IGameManager _gameManager;
    private int _coins;
    private int _countOfPoints;
    private IReadOnlyList<PowerUpData> _powerUps = System.Array.Empty<PowerUpData>();

    public HumanP2Player(OtherPlayer otherPlayer, IGameManager gameManager, FreezePlayerMono freezePlayerMono,
        BallZone ballZone)
    {
        _otherPlayer = otherPlayer;
        _freezePlayerMono = freezePlayerMono;
        _balance = otherPlayer.Balance;
        _ballZone = ballZone;
        // "2" is unique against P1's "1" and the AI's "Machine": CommandPlayerQueue routes by Id.
        Id = "2";
        _commandQueue = new CommandQueue();
        // OnBalanceClicked is a field of the concrete Balance component, not of IBalance: casting here
        // keeps the interface unchanged. A tap on P2's paths is this mediator's own input funnel.
        if (_balance is Balance balanceComponent) balanceComponent.OnBalanceClicked += OnBalanceClicked;
        else Debug.LogError("[HumanP2Player] P2 balance is not a concrete Balance; P2 cannot tap its paths.");
        _balance.OnGetPoints += OnGetPoints;
    }

    public string Id { get; }

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

    public void Configure(IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps)
    {
        _gameManager = gameManager;
        _powerUps = powerUps;
        // Deliberately starts nothing: a human P2 has no AI to boot.
    }

    public void ConfigureBallZone(IReadOnlyList<PowerUpData> powerUps)
    {
        if (_ballZone == null)
        {
            // The serialized P2 zone was not a concrete BallZone: fail closed, never unlimited.
            Debug.LogError("[HumanP2Player] P2 has no concrete BallZone; the board cannot play nor spawn orbs.");
            return;
        }

        _balance.Configure(this);
        _ballZone.Configure(this, _gameManager, powerUps);
    }

    public void EnableControls()
    {
        if (_isGameOver) return;
        _balance.EnableControls();
        _ballZone.EnableControls();
    }

    public void DisableControls()
    {
        _balance.DisableControls();
        _ballZone.DisableControls();
    }

    public void ConsumePowerUp(string prefabID)
    {
        // Local PvP never spends the account inventory. The match-local budget replaces this in S4.
        Debug.Log($"[HumanP2Player] power-up '{prefabID}' used; account inventory untouched.");
    }

    public IReadOnlyList<PowerUpData> GetPowerUps()
    {
        return _powerUps;
    }

    public void AddPoints(int points)
    {
        _coins += points;
        var scoreText = _otherPlayer.ScoreText;
        if (scoreText != null)
        {
            scoreText.text = $"{_coins}";
        }
        else
        {
            Debug.LogWarning($"[HumanP2Player] ScoreText is not wired; P2 points are {_coins}.");
        }
    }

    public int GetCoins()
    {
        return _coins;
    }

    public void GameOver()
    {
        _isGameOver = true;
    }

    public bool IsGameOver()
    {
        return _isGameOver;
    }

    public void ReadyToPlay()
    {
        _readyToPlay = true;
    }

    public bool IsReadyToPlay()
    {
        return _readyToPlay;
    }

    public IBalance GetBalance()
    {
        return _balance;
    }

    public IBallZone GetBallZone()
    {
        return _ballZone;
    }

    public IGameManager GetGameManager()
    {
        return _gameManager;
    }

    public IPlayer GetPlayer()
    {
        return this;
    }

    public void FreezeGame(bool isFreeze)
    {
        if (isFreeze)
        {
            _freezePlayerMono.Freeze();
        }
        else
        {
            _freezePlayerMono.Unfreeze();
        }
    }

    private void OnBalanceClicked(PathType sidePathType)
    {
        if (_isGameOver) return;
        if (_ballZone == null) return;
        if (ServiceLocator.Instance.GetService<ICommandQueue>().IsBusy(this)) return;
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new TryBallInBalanceCommand(_ballZone, sidePathType, _balance), this);
    }

    private void OnGetPoints()
    {
        _countOfPoints++;
        if (_countOfPoints < ServiceLocator.Instance.GetService<IGameConfigurationUseCase>().GetConfig()
                .how_much_match_to_get_power_up
            || _ballZone == null
            || !_ballZone.CanCreatePowerUp()
            || GetPowerUps().Count <= 0
        ) return;
        _countOfPoints = 0;
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("FeedPowerUp");
        _ballZone.CreatePowerUp();
    }
}
