using System.Linq;
using UnityEngine;

public class PowerUpsFactory
{
    private readonly PowerUpsConfiguration powerUpsConfiguration;

    public PowerUpsFactory(PowerUpsConfiguration powerUpsConfiguration)
    {
        this.powerUpsConfiguration = powerUpsConfiguration;
    }

    public PowerUpA Create(string id)
    {
#if UNITY_EDITOR
        var powerUpsData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().GetPowerUpsDummy();
        //id = "joker";
#else
        var powerUpsData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData().GetPowerUps();
#endif
        if (powerUpsData.Count <= 0)
        {
            return null;
        }

        //if string is 'random' get a random powerup
        if (id == "random")
        {
            var randomIndex = Random.Range(0, powerUpsData.Count);
            id = powerUpsData[randomIndex].id;
        }

        var prefab = powerUpsData.FirstOrDefault(powerUp => powerUp.id == id);
        return prefab == null
            ? null
            : Object.Instantiate(powerUpsConfiguration.GetPowerUpPrefabById(prefab.id));
    }
}