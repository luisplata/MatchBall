using Commander;
using Commander.Commands;
using UnityEngine;

public class PowerUpAIceBall : PowerUpA
{
    [SerializeField] private float timeToFreeze;

    protected override void OnActionWithClick()
    {
        var otherPlayer = _ballZone.GetPlayer().GetGameManager().GetOtherPlayer(_player);
        ServiceLocator.Instance.GetService<ICommandQueue>().ClearCommands(otherPlayer);
        ServiceLocator.Instance.GetService<ICommandQueue>()
            .AddCommand(new FreezeBall(timeToFreeze, otherPlayer), otherPlayer);
        RemoveBall();
    }
}