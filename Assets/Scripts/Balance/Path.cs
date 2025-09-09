using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

public class Path : MonoBehaviour, IPointerClickHandler
{
    public Action OnPathClicked;
    [SerializeField] private GameObject[] pathPoints = new GameObject[36];
    [SerializeField] private Ball[] ballQueue = new Ball[36];
    [SerializeField] private PathType pathType;
    [SerializeField] private GameObject pointToTransition;
    [SerializeField] private int levelOfPath;
    [SerializeField] private int _counterBalance;
    private int _diffToMove;
    private IBalance _balance;
    private IBall _ball;
    private bool _disableControls;

    public void Configure(IBalance balance)
    {
        _balance = balance;
        _disableControls = false;
    }

    public async UniTask TryBall(Ball ball, Action callback = null)
    {
        var indexToWhile = levelOfPath + _counterBalance;
        for (int i = ballQueue.Length - 1; i >= 0; i--)
        {
            if (ballQueue[i] != null)
            {
                indexToWhile = i + 2;
                break;
            }
        }

        ballQueue[indexToWhile] = ball;
        ball.OnStartMove();
        await ball.MoveBall(ball.transform.position, pointToTransition.transform.position);
        await ball.MoveBall(ball.transform.position, pathPoints[indexToWhile].transform.position, ball.ShootVfx);
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("CollisionBalls");
        callback?.Invoke();
        ball.OnFinishedMove();
    }

    public int GetBallCount()
    {
        return ballQueue.Count(ball => ball != null) + _counterBalance;
    }

    public void AddCounterBalance()
    {
        _counterBalance++;
    }

    private async UniTask MoveToAllBallsTask(int diff, bool toUp, int toStart = 0)
    {
        var listToTask = new List<UniTask>();
        if (toUp)
        {
            for (int i = ballQueue.Length - 1; i >= toStart; i--)
            {
                if (ballQueue[i])
                {
                    var ball = ballQueue[i];
                    int newIndex = i + (diff);
                    if (newIndex < ballQueue.Length)
                    {
                        listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                            pathPoints[newIndex].transform.position));
                        ballQueue[i] = null;
                        ballQueue[newIndex] = ball;
                    }
                }
            }
        }
        else
        {
            for (int i = toStart; i < ballQueue.Length; i++)
            {
                if (ballQueue[i])
                {
                    var ball = ballQueue[i];
                    int newIndex = i - diff;
                    if (newIndex >= 0)
                    {
                        listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                            pathPoints[newIndex].transform.position));
                        ballQueue[i] = null;
                        ballQueue[newIndex] = ball;
                    }
                }
            }
        }

        await DoSomethingWhileWaiting(listToTask, _balance.BallanceControllerArt.UpdatePositions);
    }

    async UniTask DoSomethingWhileWaiting(IEnumerable<UniTask> tasks, Action callback = null)
    {
        var combined = UniTask.WhenAll(tasks);

        while (!combined.Status.IsCompleted())
        {
            callback?.Invoke();
            await UniTask.Yield();
        }

        await combined;
    }


    public void RemoveCounterBalance()
    {
        _counterBalance--;
    }

    public int GetCounterBalance()
    {
        return _counterBalance;
    }

    public int GetOnlyBalls()
    {
        return ballQueue.Count(ball => ball != null);
    }

    public bool TryGetBallInPosition(int i, out Ball ball)
    {
        try
        {
            ball = ballQueue[i];
            return ball != null;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            ball = null;
            _balance.GetMediatorBalance().GameOver();
            return false;
        }
    }

    public void RemoveBall(int i)
    {
        var ball = ballQueue[i];
        if (ball.Type.Contains("_shell"))
        {
            var onlyType = ball.Type.Split('_')[0];
            ball.SetType(onlyType);
            ball.FakeRemove();
        }
        else
        {
            ball.RemoveBall();
            ballQueue[i] = null;
        }
    }

    public async UniTask MoveBallsDownByIndex(int diff, int i = 0)
    {
        await MoveToAllBallsTask(diff, false, i);
    }

    public async UniTask MoveBallsUpByIndex(int diff, int i = 0)
    {
        await MoveToAllBallsTask(diff, true, i);
    }

    public void RestartCouterBalance()
    {
        _counterBalance = 0;
    }

    public bool IsTheLastPositionFulled()
    {
        return ballQueue[^1] != null;
    }

    public void DisableControls()
    {
        _disableControls = true;
    }

    public List<Ball> GetBalls()
    {
        return ballQueue.Where(v => v).ToList();
    }

    public async UniTask DeleteColor(string colorToDelete, GameObject vfxToDestroyBalls)
    {
        for (int i = 0; i < ballQueue.Length; i++)
        {
            if (ballQueue[i] == null) continue;
            if (ballQueue[i].GetColor() == colorToDelete)
            {
                ballQueue[i].SetVfxToDestroyBalls(vfxToDestroyBalls);
                ballQueue[i].RemoveBall();
                ballQueue[i] = null;
                await MoveBallsDownByIndex(2, i);
            }
        }

        await UniTask.DelayFrame(100);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_disableControls) return;
        OnPathClicked?.Invoke();
    }

    public async UniTask MoveBallsDownByIndexAsc(int diff, int index = 0)
    {
        var listToTask = new List<UniTask>();

        for (int i = index; i < ballQueue.Length; i++)
        {
            if (ballQueue[i])
            {
                var ball = ballQueue[i];
                int newIndex = i - diff;
                if (newIndex >= 0)
                {
                    listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                        pathPoints[newIndex].transform.position));
                    ballQueue[i] = null;
                    ballQueue[newIndex] = ball;
                }
            }
        }

        await UniTask.WhenAll(listToTask);
    }

    public async UniTask MoveBallsUpByIndexAsc(int diff, int index)
    {
        var listToTask = new List<UniTask>();

        for (int i = ballQueue.Length - 1; i >= index; i--)
        {
            if (ballQueue[i])
            {
                var ball = ballQueue[i];
                int newIndex = i + (diff);
                if (newIndex < ballQueue.Length)
                {
                    listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                        pathPoints[newIndex].transform.position));
                    ballQueue[i] = null;
                    ballQueue[newIndex] = ball;
                }
            }
        }

        await UniTask.WhenAll(listToTask);
    }


    public async UniTask MoveBallsDownByIndexDesc(int diff, int index)
    {
        var listToTask = new List<UniTask>();

        for (int i = ballQueue.Length - 1; i >= index; i--)
        {
            if (ballQueue[i])
            {
                var ball = ballQueue[i];
                int newIndex = i - diff;
                if (newIndex >= 0)
                {
                    listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                        pathPoints[newIndex].transform.position));
                    ballQueue[i] = null;
                    ballQueue[newIndex] = ball;
                }
            }
        }

        await UniTask.WhenAll(listToTask);
    }

    public async UniTask MoveBallsUpByIndexDesc(int diff, int index)
    {
        var listToTask = new List<UniTask>();

        for (int i = ballQueue.Length - 1; i >= index; i--)
        {
            if (ballQueue[i])
            {
                var ball = ballQueue[i];
                int newIndex = i + diff;
                if (newIndex < ballQueue.Length)
                {
                    listToTask.Add(ball.MoveBall(pathPoints[i].transform.position,
                        pathPoints[newIndex].transform.position));
                    ballQueue[i] = null;
                    ballQueue[newIndex] = ball;
                }
            }
        }

        await UniTask.WhenAll(listToTask);
    }

    public void SetCounterBalance(int newCounterBalance)
    {
        _counterBalance = newCounterBalance;
    }

    public int GetLastIndexBall()
    {
        for (int i = ballQueue.Length - 1; i >= 0; i--)
        {
            if (ballQueue[i])
            {
                return i;
            }
        }

        return -1;
    }

    public Ball ExtractBall(int lastBallIndex)
    {
        var ball = ballQueue[lastBallIndex];
        ballQueue[lastBallIndex] = null;
        return ball;
    }

    public GameObject GetPointToTransition()
    {
        return pointToTransition;
    }

    public PathType GetPath()
    {
        return pathType;
    }

    public void EnableControls()
    {
        _disableControls = false;
    }

    public bool HaveSpace()
    {
        return ballQueue.Length / 2 < 36;
    }

    public Transform GetBasePosition()
    {
        return pathPoints[levelOfPath].transform;
    }

    public Ball GetLastBall()
    {
        var lastIndex = GetFirstBallIndex();
        if (lastIndex < 0) return null;
        return ballQueue[lastIndex];
    }

    private int GetFirstBallIndex()
    {
        for (int i = 0; i < ballQueue.Length; i++)
        {
            if (ballQueue[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    public void RepositionBasePath()
    {
    }
}