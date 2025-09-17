using UnityEngine;

public class PowerUpAWithClick : PowerUpA
{
    [SerializeField] private string message;
    protected override void OnActionWithClick()
    {
        _ballZone.UseDefaultPowerUp(message);
        gameObject.SetActive(false);
    }
}