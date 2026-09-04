using App.UseCase.GameConfiguration;
using Domain.Entities;
using Unity.Services.RemoteConfig;

namespace Infra.UnityConfiguration
{
    public class UnityConfigurationAdapter : IGameConfigurationAdapter
    {
        public GameConfiguration LoadConfig()
        {
            // var winExperience = RemoteConfigService.Instance.appConfig.GetInt("exp_by_win");
            // var loseExperience = RemoteConfigService.Instance.appConfig.GetInt("exp_by_lose");
            // var powerUpIcePerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_ice_per_buy");
            // var powerUpJokerPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_joker_per_buy");
            // var powerUpLeadPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_lead_per_buy");
            // var powerUpRayPerBuy = RemoteConfigService.Instance.appConfig.GetInt("powerup_ray_per_buy");
            // var randomToUsePowerUp = RemoteConfigService.Instance.appConfig.GetInt("random_to_use_power_up");
            // var howMuchMatchToGetPowerUp = RemoteConfigService.Instance.appConfig.GetInt("how_much_match_to_get_power_up");
            
            var winExperience = 150;
            var loseExperience = 100;
            var powerUpIcePerBuy = 5;
            var powerUpJokerPerBuy = 5;
            var powerUpLeadPerBuy = 5;
            var powerUpRayPerBuy = 5;
            var randomToUsePowerUp = 20;
            var howMuchMatchToGetPowerUp = 5;
            
            return new GameConfiguration(
                loseExperience,
                winExperience,
                powerUpIcePerBuy,
                powerUpJokerPerBuy,
                powerUpLeadPerBuy,
                powerUpRayPerBuy,
                howMuchMatchToGetPowerUp,
                randomToUsePowerUp
            );
        }
    }
}