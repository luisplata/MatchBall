// comment to get real powerups
// #define POWERUPS_TO_TEST

using UnityEngine;
using System.Linq;
using App.UseCase.PowerUps;
using Cysharp.Threading.Tasks;

public class PowerUpsFactory
{
    private readonly PowerUpsConfiguration powerUpsConfiguration;

    public PowerUpsFactory(PowerUpsConfiguration powerUpsConfiguration)
    {
        this.powerUpsConfiguration = powerUpsConfiguration;
    }

    public PowerUpA Create(string id)
    {
        // var powerUpsData = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetPowerUps();
#if UNITY_EDITOR && POWERUPS_TO_TEST
        var powerUpsData = ServiceLocator.Instance.GetService<IPlayerSessionService>().GetPlayerSessionData()
            .GetPowerUpsDummy();
        //id = "joker";
#else
        var powerUpsData = ServiceLocator.Instance.GetService<IPowerUpsUseCase>().GetSelectedPowerUps();
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
        
        ServiceLocator.Instance.GetService<IPowerUpsUseCase>().ConsumePowerUp(prefab.id).Forget();
        return prefab == null
            ? null
            : Object.Instantiate(powerUpsConfiguration.GetPowerUpPrefabById(prefab.id));
    }
}