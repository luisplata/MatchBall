using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public abstract class Ball : MonoBehaviour, IBall
{
    [SerializeField] private InputClickHandle inputClickHandle;
    [SerializeField] protected bool _canBeClicked;
    [SerializeField] private PathType _pathType;
    [SerializeField] private float speed = 1f;
    [SerializeField] protected string id;
    [SerializeField] protected string type;
    [SerializeField] protected GameObject art;
    [SerializeField] private VfxManagerGame vfxManagerGame;
    [SerializeField] private VfxManagerGame vfxManagerGameDestroy;

    public string Id => id;
    public GameObject BasePosition => vfxManagerGame.PointToBase;
    public SpriteRenderer Art => art.GetComponent<SpriteRenderer>();
    public string Type => type;
    protected IBallZone _ballZone;
    private Vector3 _startPosition;
    [SerializeField] protected string _GUIID;
    private bool _inQueue;

    public int indexQueueOfBalls;
    public int indexQueueOfPositions;

    public virtual void Configure(IBallZone ballZone, Vector3 startPosition, Vector3 targetPosition, float speed)
    {
        inputClickHandle.Configure(this);
        inputClickHandle.OnClickDown += OnDragFunction;
        inputClickHandle.OnClickUp += OnDropFunction;
        //_canBeClicked = true;
        _ballZone = ballZone;
        _startPosition = startPosition;
        this.speed = speed;
        MoveBall(_startPosition, targetPosition).Forget();
        _pathType = PathType.None;
        //StartConfigSelf();
        //create a unique string
        _GUIID = $"{type}_{Guid.NewGuid()}";
    }

    [ContextMenu("ShootVfx")]
    public void ShootVfx()
    {
        vfxManagerGame.StartVfx().Forget();
    }

    private void Reset()
    {
        inputClickHandle = GetComponent<InputClickHandle>();
    }

    protected virtual void StartConfigSelf()
    {
        var config = ServiceLocator.Instance.GetService<IGlobalParameters>().GetRandomBall();
        type = config.Name;
    }

    public async UniTask MoveBall(Vector3 startPosition, Vector3 targetPosition, Action callback = null)
    {
        //move the ball with dotween
        gameObject.transform.position = startPosition;
        
        await gameObject.transform.DOMove(targetPosition, speed).AsyncWaitForCompletion();
        _startPosition = targetPosition;
        callback?.Invoke();
    }

    public async UniTask MoveBall(Vector3 startPosition, Vector3 targetPosition, float speedInComming,
        Action callback = null)
    {
        var altSpeed = speed;
        speed = speedInComming;
        await MoveBall(startPosition, targetPosition, speed);
        speed = altSpeed;
    }

    public void StayInZone(PathType pathType)
    {
        _pathType = pathType;
    }

    public bool CanSetZone(PathType pathType)
    {
        return _pathType == pathType;
    }

    protected virtual void OnDropFunction()
    {
    }

    protected virtual void OnDragFunction()
    {
    }

    public bool CanBeClicked()
    {
        return _canBeClicked;
    }

    public string GetId()
    {
        return type;
    }

    public void RemoveBall()
    {
        art.SetActive(false);
        _canBeClicked = false;
        inputClickHandle.DisableCollider();
        vfxManagerGameDestroy.StartVfx().Forget();
    }

    public virtual void OnFinishedMove()
    {
        _inQueue = true;
    }

    public virtual void OnStartMove()
    {
    }

    public bool IsPowerUp()
    {
        var powerUp = this is IPowerUp;
        return powerUp;
    }

    public string GetColor()
    {
        return type;
    }

    public bool InQueue()
    {
        return _inQueue;
    }

    public void SetVfxToDestroyBalls(GameObject vfxToDestroyBalls)
    {
        vfxManagerGameDestroy.SetVfx(vfxToDestroyBalls);
    }

    public void SetType(string onlyType)
    {
        type = onlyType;
    }

    public virtual void FakeRemove()
    {
    }

    public void SetScale(float newScale)
    {
        transform.localScale = new Vector3(newScale, newScale, newScale);
    }
}

public interface IBall
{
    bool CanBeClicked();
}