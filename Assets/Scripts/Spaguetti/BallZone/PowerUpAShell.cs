using Commander;
using Commander.Commands;
using UnityEngine;

public class PowerUpAShell : PowerUpA
{
    [SerializeField] private SpriteRenderer imageToShell;
    [SerializeField] private float scaleToShell;

    public override void Configure(IBallZone ballZone, Vector3 startPosition, Vector3 targetPosition, float speed,
        IPlayer player)
    {
        base.Configure(ballZone, startPosition, targetPosition, speed, player);
        StartConfigSelf();
    }

    protected override void OnActionWithClick()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(
                new SendShellBallToEnemy(this, _ballZone.GetBallZone().GetBalance(), scaleToShell,
                    imageToShell.gameObject, _player), _ballZone.GetPlayer());
    }

    protected override void StartConfigSelf()
    {
        var ballToGetImageAndColor = _ballZone.GetBallFactory().CreateBall();
        art.GetComponent<SpriteRenderer>().sprite = ballToGetImageAndColor.Art.sprite;
        type = $"{ballToGetImageAndColor.Type}_shell";
        Destroy(ballToGetImageAndColor.gameObject);
    }

    public override void FakeRemove()
    {
        imageToShell.enabled = false;
    }
}