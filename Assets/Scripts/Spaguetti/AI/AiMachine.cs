using System;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

public class AiMachine : IAiMachine
{
    private readonly int _randomToUsePowerUp;
    private bool _canPlay = true;
    private TeaTime _waitFirstPlayerMove, _tryBallInRandomPath, _waitTimeState, _checkIfICanUsePowerUp, _usePowerUp;
    private float _deltaLocal = 0f;

    public AiMachine(IPlayer player, IGameManager gameManager, int randomToUsePowerUp)
    {
        _randomToUsePowerUp = randomToUsePowerUp;
        _waitFirstPlayerMove = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _tryBallInRandomPath = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _waitTimeState = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _checkIfICanUsePowerUp = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());
        _usePowerUp = new TeaTime(ServiceLocator.Instance.GetService<ITeaTimeManager>().GetMonoBehaviour());

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
                _checkIfICanUsePowerUp.Play();
            }
        });
        _tryBallInRandomPath.Pause().Add(() =>
        {
            //get random path
            PathType path;
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
            else
            {
                var randomPath = Random.Range(0, 2);
                path = randomPath == 0 ? PathType.Left : PathType.Right;
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

        _checkIfICanUsePowerUp.Pause().Add(() =>
        {
            //Validate if I can use the powerup
            if (player.GetBallZone().IsHavePowerUps() && DiceTry())
            {
                _usePowerUp.Play();
            }
            else
            {
                _tryBallInRandomPath.Play();
            }
        });

        _usePowerUp.Pause()
            .Add(() =>
            {
                Debug.Log($"[AiMachine] I can use Powerup!");
                player.GetBallZone().GetFirstPowerUp().Click();
            })
            .Add(() => { _waitTimeState.Play(); });
    }

    private static bool DiceTry()
    {
        var range = Random.Range(0, 100);
        Debug.Log($"range is {range}");
#if UNITY_EDITOR
        return range <= 90;
#else
        return range >= _randomToUsePowerUp;
#endif
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