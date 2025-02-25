using Commander;
using Commander.Commands;

public class PowerUpJoker : PowerUp
{
    protected override void OnActionWithClick()
    {
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new AddPowerUpJokerInQueue(_ballZone, this), _ballZone.GetPlayer());
    }

    protected override void StartConfigSelf()
    {
        type = "all";
    }
}