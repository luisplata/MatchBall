using Commander;
using Commander.Commands;

public class PowerUpLeadBall : PowerUp
{
    protected override void OnActionWithClick()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new UseLeadPowerUpInPath(this, _ballZone.GetBallZone().GetBalance()), _ballZone.GetPlayer());
    }

    protected override void StartConfigSelf()
    {
        type = "all";
    }
}