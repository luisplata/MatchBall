using System.Collections;
using System.Collections.Generic;
using Commander;
using Commander.Commands;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class BallZone : MonoBehaviour, IBallZone
{
    [SerializeField] private List<GameObject> ballPoints;
    [SerializeField] private List<GameObject> powerUpsPoints;
    [SerializeField] private GameObject ballPointToSpawn;

    [SerializeField] private BallFactory ballFactory;
    [SerializeField] private PowerUpFactory powerUpFactory;

    [SerializeField] private Ball[] _balls = new Ball[4];
    [SerializeField] private PowerUpA[] _powerUps = new PowerUpA[3];
    [SerializeField] private float _speedToMove;
    [SerializeField] private float _speedToFeedBallZone;
    [SerializeField] private SwitchBalls switchBalls;
    private int _overFlow = 1;
    private int _ballIndex = -1;
    private Coroutine _fillRoutine;

    // Controls gate: a board that has been closed (game over) stops accepting taps. Outside local
    // PvP IsInputEnabled() is always true, so PvE and the tutorial keep today's semantics exactly.
    private bool _inputEnabled = true;

    private IMediatorBallZone _player;
    private IGameManager _gameManager;

    public void Configure(IMediatorBallZone player, IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps)
    {
        _player = player;
        _gameManager = gameManager;
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("Feed3Balls");
        StartFill();
        switchBalls.OnSwitchClicked += OnSwitchClicked;
        powerUpFactory.Configure(powerUps, _player.GetPlayer());
    }

    public IPlayer GetPlayer()
    {
        return _player.GetPlayer();
    }

    public IMediatorBallZone GetBallZone()
    {
        return _player;
    }

    public GameObject GetBallPointToSpawn()
    {
        return ballPointToSpawn;
    }

    public void EnableControls()
    {
        _inputEnabled = true;
    }

    public BallFactory GetBallFactory()
    {
        return ballFactory;
    }

    private void OnSwitchClicked()
    {
        if (!IsInputEnabled()) return;
        var player = _player.GetPlayer();
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new SwitchBallsCommand(_balls, ballPoints), player);
    }

    private IEnumerator FillQueueCoroutine()
    {
        while (IsNeedFillBallQueue())
        {
            FillQueue();
            yield return new WaitForSeconds(_speedToFeedBallZone);
        }

        _fillRoutine = null;
    }

    private void StartFill()
    {
        if (_fillRoutine != null)
        {
            StopCoroutine(_fillRoutine);
            _fillRoutine = null;
        }

        _fillRoutine = StartCoroutine(FillQueueCoroutine());
    }

    private bool IsNeedFillBallQueue()
    {
        return _ballIndex < _balls.Length - _overFlow - 1;
    }

    public Ball GetFirstBall()
    {
        var ball = _balls[0];
        _balls[0] = null;
        MoveBallsIndex();
        _ballIndex--;
        ServiceLocator.Instance.GetService<IMusicController>().PlaySfx("FeedOneBall");
        StartFill();
        if (ball.IsPowerUp())
        {
            IncrementOverFlow();
        }

        return ball;
    }

    public void DeleteAllBallsWithHaveMoreSameColor(GameObject vfxToDestroyBalls)
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new DeleteColorLargestNumberOfBallsCommand(_player.GetBalance(), vfxToDestroyBalls),
                _player.GetPlayer());
    }

    public void UseDefaultPowerUp(string messageFromPowerUp)
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new ShowMessageInScreenCommand(_gameManager, messageFromPowerUp), _player.GetPlayer());
    }

    public int GetCountOfBalls()
    {
        return _balls.Length - _overFlow - 1;
    }

    public GameObject GetBallPositionGameObject(int i)
    {
        return ballPoints[i];
    }

    public void SetBall(int i, Ball ball)
    {
        _balls[i] = ball;
    }

    public Ball GetLastBall()
    {
        return _balls[^1];
    }

    public void SetLastBall(Ball o)
    {
        _balls[^1] = o;
    }

    public Ball GetBall(int i)
    {
        return _balls[i];
    }

    private void FillQueue()
    {
        _ballIndex++;
        //var ballInstance = Instantiate(ballPrefab, ballPointToSpawn.transform.position, Quaternion.identity);
        var ballInstance = ballFactory.CreateBall();
        ballInstance.Configure(this, ballPointToSpawn.transform.position, ballPoints[_ballIndex].transform.position,
            _speedToMove, _player.GetPlayer());
        //ballInstance.MoveBall(ballPointToSpawn.transform.position, ballPoints[_ballIndex].transform.position);
        _balls[_ballIndex] = ballInstance;
        if (_ballIndex >= _balls.Length - _overFlow) _ballIndex = _balls.Length - _overFlow - 1;
    }

    private void MoveBallsIndex()
    {
        for (int i = 0; i < _balls.Length - _overFlow; i++)
        {
            var ball = _balls[i];
            if (ball == null) continue;
            var newInd = i - 1;
            if (newInd < 0) continue;
            ball.MoveBall(ball.transform.position, ballPoints[newInd].transform.position).Forget();
            _balls[newInd] = ball;
        }

        _balls[_ballIndex] = null;
    }

    public void DisableControls()
    {
        _inputEnabled = false;
    }

    /// <summary>
    /// Single input gate consulted by the switch handler and by the two mediators' path funnels
    /// (the orb handler reads it too). Outside local PvP every read is true on purpose: without the
    /// two escape clauses the flag would silently harden ICE's freeze in PvE (it would freeze the
    /// orbs and the switch as well as the paths) and nothing would catch it.
    /// </summary>
    public bool IsInputEnabled()
    {
        return _inputEnabled || _gameManager == null || !_gameManager.IsLocalPvP();
    }

    public void CreatePowerUp()
    {
        var powerUpInstance = powerUpFactory.CreatePowerUp();
        if (powerUpInstance == null) return;
        for (int i = 0; i < _powerUps.Length; i++)
        {
            if (_powerUps[i] == null)
            {
                powerUpInstance.Configure(this, ballPointToSpawn.transform.position,
                    powerUpsPoints[i].transform.position, _speedToMove, _player.GetPlayer());
                _powerUps[i] = powerUpInstance;
                break;
            }
        }
    }

    public bool CanCreatePowerUp()
    {
        return _powerUps[^1] == null;
    }

    public bool IsHavePowerUps()
    {
        return GetFirstPowerUp() != null;
    }

    public PowerUpA GetFirstPowerUp()
    {
        return _powerUps[0];
    }

    public bool UsePowerUp(IPowerUp powerUpWithClick)
    {
        for (var i = 0; i < _powerUps.Length; i++)
        {
            var powerUp = _powerUps[i];
            if (powerUp != null && powerUp.GetIdUnique() == powerUpWithClick.GetIdUnique())
            {
                _powerUps[i] = null;
                // The orb left the board: this is the one and only place a use is charged.
                // The slot is already null, so a second activation cannot find it and cannot charge again.
                GetPlayer().ConsumePowerUp(powerUp.GetBall().Id);
                // Move all powerUps down
                for (var j = i; j < _powerUps.Length - 1; j++)
                {
                    if (_powerUps[j + 1] != null)
                    {
                        _powerUps[j + 1].MoveBall(_powerUps[j + 1].transform.position,
                            powerUpsPoints[j].transform.position).Forget();
                        _powerUps[j] = _powerUps[j + 1];
                        _powerUps[j + 1] = null;
                    }
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Orbs of this type currently on the board (this match only). Derived from occupancy on purpose:
    /// a stored counter would be a second source of truth and could drift.
    /// </summary>
    public int CountReservedPowerUps(string id)
    {
        var reserved = 0;
        for (var i = 0; i < _powerUps.Length; i++)
        {
            var powerUp = _powerUps[i];
            if (powerUp != null && powerUp.GetBall().Id == id) reserved++;
        }

        return reserved;
    }

    public void AddPowerUpInQueue(IPowerUp powerUpJoker)
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new AddPowerUpJokerInQueue(this, powerUpJoker), _player.GetPlayer());
    }

    public void IncrementOverFlow()
    {
        _overFlow++;
    }

    public void DecrementOverFlow()
    {
        _overFlow--;
    }

    public bool CanUsePowerUp()
    {
        return _overFlow > 0;
    }
}

public interface IBallZone
{
    bool UsePowerUp(IPowerUp powerUpWithClick);
    void AddPowerUpInQueue(IPowerUp powerUpJoker);
    void IncrementOverFlow();
    void DecrementOverFlow();
    bool CanUsePowerUp();
    Ball GetFirstBall();
    void DeleteAllBallsWithHaveMoreSameColor(GameObject vfxToDestroyBalls);
    void UseDefaultPowerUp(string messageFromPowerUp);
    int GetCountOfBalls();
    Ball GetBall(int i);
    GameObject GetBallPositionGameObject(int i);
    void SetBall(int i, Ball ball);
    Ball GetLastBall();
    void SetLastBall(Ball o);
    void DisableControls();
    void Configure(IMediatorBallZone player, IGameManager gameManager, IReadOnlyList<PowerUpData> powerUps);
    bool IsInputEnabled();
    IPlayer GetPlayer();
    IMediatorBallZone GetBallZone();
    GameObject GetBallPointToSpawn();
    void EnableControls();
    BallFactory GetBallFactory();
    void CreatePowerUp();
    bool CanCreatePowerUp();
    bool IsHavePowerUps();
    PowerUpA GetFirstPowerUp();
}