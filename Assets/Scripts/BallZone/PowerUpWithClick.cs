using UnityEngine;

public class PowerUpWithClick : PowerUp
{
    [SerializeField] private string message;
    protected override void OnActionWithClick()
    {
        _ballZone.UseDefaultPowerUp(message);
        gameObject.SetActive(false);
    }
}