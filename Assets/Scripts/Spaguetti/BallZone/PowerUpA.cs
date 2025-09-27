using UnityEngine;

public abstract class PowerUpA : Ball, IPowerUp
{
    public override void Configure(IBallZone ballZone, Vector3 startPosition, Vector3 targetPosition, float speed)
    {
        _canBeClicked = true;
        base.Configure(ballZone, startPosition, targetPosition, speed);
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
}

public interface IPowerUp
{
    string GetId();
    Ball GetBall();
    string GetIdUnique();
}