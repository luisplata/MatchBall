using System.Collections.Generic;
using UnityEngine;

public class PowerUpFactory : MonoBehaviour
{
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    private PowerUpsFactory _powerUpsFactory;
    private PowerUpSpawner _powerUpSpawner;

    public void Configure(IReadOnlyList<PowerUpData> powerUps, IPlayer player)
    {
        _powerUpsFactory = new PowerUpsFactory(Instantiate(powerUpsConfiguration), powerUps, player);
        _powerUpSpawner = new PowerUpSpawner(_powerUpsFactory);
    }

    public PowerUpA CreatePowerUp()
    {
        return _powerUpSpawner.SpawnPowerUp();
    }
}