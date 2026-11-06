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
        // The inactive board's orbs are dead to the touch: no effect, no command, no turn pass.
        if (_ballZone == null || !_ballZone.IsInputEnabled()) return;
        if (_ballZone.CanUsePowerUp() && _ballZone.UsePowerUp(this))
        {
            OnActionWithClick();
            // Only inside the success branch: RAY adds no command of its own and a rejected drag
            // must not hand the move to the opponent.
            _player?.GetGameManager()?.NotifyPlayerActed(_player);
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