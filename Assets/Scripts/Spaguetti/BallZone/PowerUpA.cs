using UnityEngine;

public abstract class PowerUpA : Ball, IPowerUp
{
    public override void Configure(IBallZone ballZone, Vector3 startPosition, Vector3 targetPosition, float speed, IPlayer player)
    {
        _canBeClicked = true;
        base.Configure(ballZone, startPosition, targetPosition, speed, player);
    }


    protected override void OnDragFunction()
    {
        base.OnDragFunction();
        if (_ballZone.CanUsePowerUp())
        {
            _ballZone.UsePowerUp(this);
            OnActionWithClick();
        }
    }

    protected abstract void OnActionWithClick();

    public Ball GetBall()
    {
        return this;
    }

    public string GetIdUnique()
    {
        return _GUIID;
    }

    public Sprite Art => art.GetComponent<SpriteRenderer>().sprite;

    public void Click()
    {
        OnDragFunction();
    }
}

public interface IPowerUp
{
    string GetId();
    Ball GetBall();
    string GetIdUnique();
}