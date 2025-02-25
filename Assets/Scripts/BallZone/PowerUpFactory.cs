using UnityEngine;

public class PowerUpFactory : MonoBehaviour
{
    [SerializeField] private PowerUpsConfiguration powerUpsConfiguration;
    private PowerUpsFactory _powerUpsFactory;
    private PowerUpSpawner _powerUpSpawner;

    private void Start()
    {
        _powerUpsFactory = new PowerUpsFactory(Instantiate(powerUpsConfiguration));
        _powerUpSpawner =
            new PowerUpSpawner(
                _powerUpsFactory);
    }

    public PowerUp CreatePowerUp()
    {
        return _powerUpSpawner.SpawnPowerUp();
    }
}