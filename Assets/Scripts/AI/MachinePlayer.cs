using System.Collections.Generic;
using Commander;
using Cysharp.Threading.Tasks;
using UnityEngine;

internal class MachinePlayer : IPlayer, IMediatorBalance, IMediatorBallZone
{
    private readonly OtherPlayer _otherPlayer;
    private readonly FreezePlayerMono _freezePlayerMono;
    private readonly IBalance _balance;
    private readonly IBallZone _ballZone;

    private bool _isGameOver;
    private IGameManager _gameManager;
    private int _coins;
    private IAiMachine _aiMachine;
    private readonly CommandQueue _commandQueue;

    public MachinePlayer(OtherPlayer otherPlayer, IGameManager gameManager, FreezePlayerMono freezePlayerMono)
    {
        _otherPlayer = otherPlayer;
        _freezePlayerMono = freezePlayerMono;
        _balance = otherPlayer.Balance;
        _ballZone = otherPlayer.BallZone;
        Id = "Machine";
        _aiMachine = new AiMachine(this, gameManager);
        _commandQueue = new CommandQueue();
    }

    public string Id { get; }
    public void AddCommand(ICommand command)
    {
        _commandQueue.AddCommand(command);
    }

    public bool IsBusy()
    {
        return !_aiMachine.CanPlay();
    }

    public void ClearCommands()
    {
        _commandQueue.ClearCommands();
    }

    public void EnableControls()
    {
        _balance.EnableControls();
        _ballZone.EnableControls();
        _aiMachine.SetCanPlay(true);
    }

    public void Configure(IGameManager gameManager)
    {
        _gameManager = gameManager;
        _aiMachine.StartAI();
    }

    public int GetCoins()
    {
        return _coins;
    }

    public bool IsGameOver()
    {
        return _isGameOver;
    }

    public void DisableControls()
    {
        _balance.DisableControls();
        _ballZone.DisableControls();
        _aiMachine.SetCanPlay(false);
    }

    public void ConfigureBallZone()
    {
        //Debug.Log("ConfigureBallZone");
        _balance.Configure(this);
        _ballZone.Configure(this, _gameManager);
    }

    public void AddPoints()
    {
        _coins++;
        //scoreText.text = $"{_coins}";
    }

    public void GameOver()
    {
        _isGameOver = true;
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
}