using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine.InputSystem;
#endif

public class Balance : MonoBehaviour, IBalance
{
    public Action<PathType> OnBalanceClicked;

    // public Action OnGetPoints;
    [SerializeField] private Path leftPath;
    [SerializeField] private Path rightPath;
    [SerializeField] private CheckerOfBallsToGetPoints checkerOfBallsToGetPoints;
    private List<BallanceControllerArt> ballancesControllerArt = new();
    private IMediatorBalance _player;
    private int _countOfColumns = 36;

    public void Configure(IMediatorBalance player)
    {
        _player = player;
        leftPath.Configure(this);
        rightPath.Configure(this);
        checkerOfBallsToGetPoints.Configure();
        // define the paths
        leftPath.OnPathClicked += () => OnBalanceClicked?.Invoke(PathType.Left);
        rightPath.OnPathClicked += () => OnBalanceClicked?.Invoke(PathType.Right);
        foreach (var ballanceControllerArt in ballancesControllerArt)
        {
            ballanceControllerArt.UpdatePositions();
        }
    }
#if UNITY_EDITOR || UNITY_STANDALONE
    private void Update()
    {
        if (Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            OnBalanceClicked?.Invoke(PathType.Left);
        }

        if (Keyboard.current.dKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            OnBalanceClicked?.Invoke(PathType.Right);
        }
    }
#endif

    public async UniTask TryBall(Ball ball, PathType sidePathType)
    {
        if (!leftPath.HaveSpace() || !rightPath.HaveSpace())
        {
            _player.GameOver();
            return;
        }

        if (sidePathType == PathType.Left)
        {
            await leftPath.TryBall(ball);
        }
        else if (sidePathType == PathType.Right)
        {
            await rightPath.TryBall(ball);
        }

        await BalanceBothPaths();
    }

    public IPlayer GetPlayer()
    {
        return _player.GetPlayer();
    }

    public string GetTheColorOfLargestNumberOfBalls()
    {
        var colorCount = leftPath.GetBalls().Concat(rightPath.GetBalls())
            .GroupBy(ball => ball.GetColor())
            .OrderByDescending(group => group.Count())
            .FirstOrDefault()?.Key;
        return colorCount ?? string.Empty;
    }

    public async UniTask DeleteColor(string colorToDelete, GameObject vfxToDestroyBalls)
    {
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("PowerUpRay");
        await UniTask.WhenAll(leftPath.DeleteColor(colorToDelete, vfxToDestroyBalls),
            rightPath.DeleteColor(colorToDelete, vfxToDestroyBalls));
        await BalanceBothPaths();
    }

    public UniTask MoveBallsDownByIndexAsc(int diff, int index)
    {
        return UniTask.WhenAll(leftPath.MoveBallsDownByIndexAsc(diff, index),
            rightPath.MoveBallsDownByIndexAsc(diff, index));
    }

    public UniTask MoveBallsDownByIndexDesc(int diff, int index)
    {
        return UniTask.WhenAll(leftPath.MoveBallsDownByIndexDesc(diff, index),
            rightPath.MoveBallsDownByIndexDesc(diff, index));
    }

    public UniTask MoveBallsUpByIndexDesc(int diff, int index)
    {
        return UniTask.WhenAll(leftPath.MoveBallsUpByIndexDesc(diff, index),
            rightPath.MoveBallsUpByIndexDesc(diff, index));
    }

    public UniTask MoveBallsUpByIndexAsc(int diff, int index)
    {
        return UniTask.WhenAll(leftPath.MoveBallsUpByIndexAsc(diff, index),
            rightPath.MoveBallsUpByIndexAsc(diff, index));
    }

    public async UniTask BalanceBothPaths()
    {
        while (true)
        {
            // Log the current state for debugging
            //Debug.Log($"Left Path Balls: {leftPath.GetBallCount()}, Right Path Balls: {rightPath.GetBallCount()}");
            //Debug.Log($"Left Path Counter Balance: {leftPath.GetCounterBalance()}, Right Path Counter Balance: {rightPath.GetCounterBalance()}");

            // Validate initial state
            if (leftPath.GetCounterBalance() < 0 || rightPath.GetCounterBalance() < 0)
            {
                Debug.LogError("Counter balance cannot be negative.");
                _player.GameOver();
                break;
            }

            //if is equals balls but not counter balance
            if (leftPath.GetOnlyBalls() == rightPath.GetOnlyBalls())
            {
                while (leftPath.GetCounterBalance() - rightPath.GetCounterBalance() != 0)
                {
                    if (leftPath.GetCounterBalance() > rightPath.GetCounterBalance())
                    {
                        await UniTask.WhenAll(
                            leftPath.MoveBallsDownByIndex(1),
                            rightPath.MoveBallsUpByIndex(1)
                        );
                        leftPath.RemoveCounterBalance();
                    }
                    else
                    {
                        await UniTask.WhenAll(
                            rightPath.MoveBallsDownByIndex(1),
                            leftPath.MoveBallsUpByIndex(1)
                        );
                        rightPath.RemoveCounterBalance();
                    }
                }

                leftPath.RestartCouterBalance();
                rightPath.RestartCouterBalance();
            }
            // if the left path has more balls than the right path
            else if (leftPath.GetBallCount() < rightPath.GetBallCount())
            {
                leftPath.AddCounterBalance();
                await UniTask.WhenAll(
                    leftPath.MoveBallsUpByIndex(1),
                    rightPath.MoveBallsDownByIndex(1)
                );
            }
            // if the right path has more balls than the left path
            else if (leftPath.GetBallCount() > rightPath.GetBallCount())
            {
                rightPath.AddCounterBalance();
                await UniTask.WhenAll(
                    rightPath.MoveBallsUpByIndex(1),
                    leftPath.MoveBallsDownByIndex(1)
                );
            }


            if (await checkerOfBallsToGetPoints.HaveCombinations(this))
            {
                continue;
            }

            //check if the balls dont overlap the max count and min count
            //max balls
            if (leftPath.IsTheLastPositionFulled() || rightPath.IsTheLastPositionFulled())
            {
                //Debug.Log("GameOver too many balls");
                _player.GameOver();
            }

            //count the counter balance
            if (Mathf.Abs(leftPath.GetOnlyBalls() - rightPath.GetOnlyBalls()) > 3)
            {
                //Debug.Log("GameOver too many counter balance");
                _player.GameOver();
            }

            if (Mathf.Abs(leftPath.GetOnlyBalls() - rightPath.GetOnlyBalls()) >= 3)
            {
                //Debug.Log("Before to GameOver too many counter balance");
            }

            if (leftPath.GetBallCount() == rightPath.GetBallCount())
            {
                // Adjust counter balance if necessary
                int ballDifference = leftPath.GetOnlyBalls() - rightPath.GetOnlyBalls();
                if (ballDifference > 0)
                {
                    leftPath.SetCounterBalance(0);
                    rightPath.SetCounterBalance(ballDifference);
                }
                else if (ballDifference < 0)
                {
                    rightPath.SetCounterBalance(0);
                    leftPath.SetCounterBalance(-ballDifference);
                }
                else
                {
                    leftPath.SetCounterBalance(0);
                    rightPath.SetCounterBalance(0);
                }

                break;
            }
        }

        foreach (var ballanceControllerArt in ballancesControllerArt)
        {
            ballanceControllerArt.UpdatePositions();
        }
    }

    public void EnableControls()
    {
        leftPath.EnableControls();
        rightPath.EnableControls();
    }

    public IMediatorBalance GetMediatorBalance()
    {
        return _player;
    }

    public void BallancesControllerArtUpdatePositions()
    {
        foreach (var ballanceControllerArt in ballancesControllerArt)
        {
            ballanceControllerArt.UpdatePositions();
        }
    }

    public Action OnGetPoints { get; set; }

    public Path GetLeftPath()
    {
        return leftPath;
    }

    public Path GetRightPath()
    {
        return rightPath;
    }

    public int CountOfColumns()
    {
        return _countOfColumns;
    }

    public async UniTask MoveBallsDownByIndex(int diff, int i = 0)
    {
        await UniTask.WhenAll(leftPath.MoveBallsDownByIndex(diff), rightPath.MoveBallsDownByIndex(diff));
    }

    public async UniTask MoveBallsUpByIndex(int diff, int i = 0)
    {
        await UniTask.WhenAll(leftPath.MoveBallsUpByIndex(diff), rightPath.MoveBallsUpByIndex(diff));
    }

    public void AddPoints()
    {
        _player.AddPoints();
        OnGetPoints?.Invoke();
    }

    public void DisableControls()
    {
        leftPath.DisableControls();
        rightPath.DisableControls();
    }

    public void AddBallanceArt(BallanceControllerArt ballanceControllerArt)
    {
        ballancesControllerArt.Add(ballanceControllerArt);
    }
}

public interface IBalance
{
    Path GetLeftPath();
    Path GetRightPath();
    int CountOfColumns();
    UniTask MoveBallsDownByIndex(int diff, int i = 0);
    UniTask MoveBallsUpByIndex(int diff, int i = 0);
    void AddPoints();
    UniTask TryBall(Ball currentBall, PathType sidePathType);
    IPlayer GetPlayer();
    string GetTheColorOfLargestNumberOfBalls();
    UniTask DeleteColor(string colorToDelete, GameObject vfxToDestroyBalls);
    UniTask MoveBallsDownByIndexAsc(int diff, int index);
    UniTask MoveBallsUpByIndexAsc(int diff, int index);
    UniTask MoveBallsDownByIndexDesc(int diff, int index);
    UniTask MoveBallsUpByIndexDesc(int diff, int index);
    void DisableControls();
    void Configure(IMediatorBalance player);
    UniTask BalanceBothPaths();
    void EnableControls();
    IMediatorBalance GetMediatorBalance();
    void BallancesControllerArtUpdatePositions();
    Action OnGetPoints { get; set; }
}

public enum PathType
{
    None,
    Left,
    Right
}