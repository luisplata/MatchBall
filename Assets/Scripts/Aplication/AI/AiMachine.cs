using System;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AiMachine : IAiMachine
{
    private bool _canPlay = true;
    private TeaTime _waitFirstPlayerMove, _tryBallInRandomPath, _waitTimeState;
    private float _deltaLocal = 0f;

    public AiMachine(IPlayer player, IGameManager gameManager)
    {
        _waitFirstPlayerMove = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _tryBallInRandomPath = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _waitTimeState = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());

        _waitFirstPlayerMove.Pause().Loop(h =>
        {
            if (gameManager.GetFirstPlayer().GetBalance().GetLeftPath().GetOnlyBalls() > 0 ||
                gameManager.GetFirstPlayer().GetBalance().GetRightPath().GetOnlyBalls() > 0)
            {
                h.Break();
            }
        }).Add(() => { _waitTimeState.Play(); });

        _waitTimeState.Pause().Add(() => { _deltaLocal = 0f; }).Loop(h =>
        {
            _deltaLocal += h.deltaTime;
            if (_canPlay && _deltaLocal >= 3f)
            {
                h.Break();
            }
        }).Add(() =>
        {
            if (!player.IsGameOver())
            {
                _tryBallInRandomPath.Play();
            }
        });
        _tryBallInRandomPath.Pause().Add(() =>
        {
            //get random path
            var randomPath = UnityEngine.Random.Range(0, 2);
            PathType path = randomPath == 0 ? PathType.Left : PathType.Right;
            if (player.GetBalance().GetLeftPath().GetOnlyBalls() - player.GetBalance().GetRightPath().GetOnlyBalls() >=
                3)
            {
                path = PathType.Right;
            }
            else if (player.GetBalance().GetLeftPath().GetOnlyBalls() -
                     player.GetBalance().GetRightPath().GetOnlyBalls() <= -3)
            {
                path = PathType.Left;
            }

            ServiceLocator.Instance.GetService<ICommandQueue>()
                .AddCommand(new TryBallInBalanceCommand(player.GetBallZone(), path, player.GetBalance()), player);
        }).Add(() =>
        {
            if (!player.IsGameOver())
            {
                _waitTimeState.Play();
            }
        });
    }

    public bool CanPlay()
    {
        return _canPlay;
    }

    public void SetCanPlay(bool canplay)
    {
        _canPlay = canplay;
    }

    public void StartAI()
    {
        _waitFirstPlayerMove.Play();
    }
}

public interface IAiMachine
{
    bool CanPlay();
    void SetCanPlay(bool canplay);
    void StartAI();
}