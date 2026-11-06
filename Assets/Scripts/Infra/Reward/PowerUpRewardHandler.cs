using System;
using App.Reward;
using App.UseCase.Reward;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Infra.Reward
{
    /// <summary>
    /// Power up reward. A "powerup" body ADDS its quantity to the inventory (the claim path uses
    /// the inventory's increment, never an assignment), and displays with the power up icon key.
    /// </summary>
    public sealed class PowerUpRewardHandler : IRewardHandler
    {
        private readonly IClaimPowerUpUseCase _claimPowerUpUseCase;

        public PowerUpRewardHandler(IClaimPowerUpUseCase claimPowerUpUseCase)
        {
            _claimPowerUpUseCase = claimPowerUpUseCase;
        }

        public string TypeId => "powerup";

        public RewardDisplayData Describe(string body)
        {
            if (!TryReadPayload(body, out var powerUpId, out var quantity))
            {
                Debug.LogWarning($"[PowerUpRewardHandler] Reward body '{body}' is incomplete; using the neutral label.");
                return new RewardDisplayData(TypeId, TypeId);
            }

            return new RewardDisplayData(powerUpId, $"Power Up {GetDisplayName(powerUpId)} x {quantity}");
        }

        public async UniTask<RewardGrantOutcome> GrantAsync(string body)
        {
            if (!TryReadPayload(body, out var powerUpId, out var quantity))
            {
                Debug.LogWarning($"[PowerUpRewardHandler] Reward body '{body}' is incomplete; grant skipped.");
                return RewardGrantOutcome.Skipped;
            }

            await _claimPowerUpUseCase.Claim(new PowerUpData { id = powerUpId, quantity = quantity });
            return RewardGrantOutcome.Granted;
        }

        private static bool TryReadPayload(string body, out string powerUpId, out int quantity)
        {
            powerUpId = null;
            quantity = 0;
            if (string.IsNullOrWhiteSpace(body)) return false;

            try
            {
                var payload = JObject.Parse(body);
                powerUpId = payload.Value<string>("powerup");
                quantity = payload.Value<int?>("quantity") ?? 0;
            }
            catch (Exception)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(powerUpId) && quantity > 0;
        }

        private static string GetDisplayName(string powerUpId)
        {
            return powerUpId switch
            {
                "POWERUP_RAY" => "Ray",
                "POWERUP_ICE" => "Ice",
                "POWERUP_LEAD" => "Lead",
                "POWERUP_JOKER" => "Joker",
                _ => "Unknown"
            };
        }
    }
}
