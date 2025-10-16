// comment to get real powerups
// #define POWERUPS_TO_TEST

using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PowerUpsFactory
{
    private readonly PowerUpsConfiguration powerUpsConfiguration;
    private readonly IReadOnlyList<PowerUpData> _powerUps;
    private readonly IPlayer _player;

    public PowerUpsFactory(PowerUpsConfiguration powerUpsConfiguration, IReadOnlyList<PowerUpData> powerUps,
        IPlayer player)
    {
        this.powerUpsConfiguration = powerUpsConfiguration;
        _powerUps = powerUps;
        _player = player;
    }

    public PowerUpA Create(string id)
    {
#if UNITY_EDITOR && POWERUPS_TO_TEST
        // var powerUpsData = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetPowerUps();
        var powerUpsData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData()
            .GetPowerUpsDummy();
        //id = "joker";
        //ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps();
#else

#endif
        var powerUpsData = _powerUps;
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

        _player.ConsumePowerUp(prefab.id);

        return prefab == null
            ? null
            : Object.Instantiate(powerUpsConfiguration.GetPowerUpPrefabById(prefab.id));
    }
}