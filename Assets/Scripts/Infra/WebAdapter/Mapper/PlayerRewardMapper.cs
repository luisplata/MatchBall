using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Infra.WebAdapter.Mapper
{
    using System.Linq;
    using Domain.Entities;

    public static class PlayerRewardMapper
    {
        private static DateTime ParseDateTimeSafely(string dateTimeString)
        {
            // Si es null o vacío, devuelve la fecha de hoy
            if (string.IsNullOrWhiteSpace(dateTimeString))
                return DateTime.Now; // O DateTime.UtcNow si trabajas con UTC

            // Intentamos parsear con el formato exacto que espera RoundtripKind (ISO 8601)
            // Ejemplo de formato: "2026-09-04T15:30:00.123Z" o "2026-09-04T15:30:00.123+02:00"
            string[] formats =
            {
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ss.fffzzz",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:sszzz",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss"
            };

            if (DateTime.TryParseExact(dateTimeString, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime result))
            {
                return result;
            }

            // Si falla, logueamos el error (opcional) y devolvemos hoy
            Debug.LogWarning($"No se pudo parsear la fecha: '{dateTimeString}'. Se usará DateTime.Now");
            return DateTime.Now;
        }

        public static PlayerPassBall ToDomain(PlayerPassBallDto dto, List<PlayerRewardDto> rewards)
        {
            return new PlayerPassBall(
                dto.id,
                dto.playerId,
                dto.level,
                dto.experience,
                ParseDateTimeSafely(dto.created_at),
                ParseDateTimeSafely(dto.updated_at),
                rewards?.Select(r => new PlayerReward(
                    r.id,
                    r.playerId,
                    r.rewardId,
                    string.IsNullOrEmpty(r.awarded_at)
                        ? DateTime.MinValue
                        : DateTime.Parse(r.awarded_at, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    r.claimed == 1,
                    r.level,
                    r.reward?.type,
                    r.reward?.body
                )).ToList()
            );
        }

        public static RewardData ToDomain(RewardDto dto)
        {
            string rewardJson = JsonUtility.ToJson(dto.reward);
            return new RewardData(
                dto.id,
                dto.level,
                rewardJson
            );
        }
    }
}