using System;
using System.Collections.Generic;
using System.Globalization;
using Domain.Entities;
using Infra.WebAdapter;
using UnityEngine;

namespace Infra.Persistence
{
    /// <summary>
    /// Single owner of the local ball pass document stored under <see cref="Key" />.
    /// The document is normalized on every read: rows come from <see cref="RewardTable" />
    /// and only their claimed flag is carried over, so missing or legacy saves migrate to a
    /// valid state instead of breaking the BallPass screen.
    /// </summary>
    public static class BallPassLocalStore
    {
        public const string Key = "playerBallPass";

        private const int FirstLevel = 1;
        private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff'Z'";

        /// <summary>Shipped table: one entry per level, in level order.</summary>
        private static readonly (int Level, string Type, string Body)[] Rewards =
        {
            (1, "gold", "{ \"amount\": 100 }"),
            (2, "gold", "{ \"amount\": 50 }"),
            (3, "powerup", "{ \"powerup\": \"POWERUP_ICE\", \"quantity\": 5 }"),
            (4, "gold", "{ \"amount\": 50 }"),
            (5, "powerup", "{ \"powerup\": \"POWERUP_JOKER\", \"quantity\": 10 }"),
            (6, "gold", "{ \"amount\": 15 }"),
            (7, "gold", "{ \"amount\": 50 }"),
            (8, "powerup", "{ \"powerup\": \"POWERUP_RAY\", \"quantity\": 5 }"),
            (9, "gold", "{ \"amount\": 50 }"),
            (10, "powerup", "{ \"powerup\": \"POWERUP_LEAD\", \"quantity\": 7 }")
        };

        private static readonly List<RewardData> Table = BuildRewardTable();

        /// <summary>Reward table handed to the catalog; treat it as read-only.</summary>
        public static IReadOnlyList<RewardData> RewardTable => Table;

        /// <summary>
        /// Reads the stored document and normalizes it. Never writes and never throws:
        /// a missing, empty, unparsable or partial document is rebuilt as the zero state.
        /// </summary>
        public static PlayerPassBallResponseDto Load()
        {
            var raw = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(raw)) return CreateZeroState();

            PlayerPassBallResponseDto document;
            try
            {
                document = JsonUtility.FromJson<PlayerPassBallResponseDto>(raw);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BallPassLocalStore] '{Key}' could not be parsed and was rebuilt. {exception.Message}");
                return CreateZeroState();
            }

            if (document?.data?.battlePass == null) return CreateZeroState();

            document.success = true;
            document.data.battlePass.level = Math.Max(document.data.battlePass.level, FirstLevel);
            document.data.battlePass.experience = Math.Max(document.data.battlePass.experience, 0);
            document.data.rewards = RebuildRows(document.data.rewards);
            return document;
        }

        /// <summary>Serializes the document and flushes PlayerPrefs.</summary>
        public static void Save(PlayerPassBallResponseDto document)
        {
            var persisted = document ?? CreateZeroState();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(persisted));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Marks the row of <paramref name="level" /> as claimed. Idempotent: an already
        /// claimed row is left untouched. Returns false when the document has no row for
        /// that level, so callers can refuse a claim for a level that is not shipped.
        /// </summary>
        public static bool MarkClaimed(PlayerPassBallResponseDto document, int level)
        {
            if (document?.data?.rewards == null) return false;

            foreach (var row in document.data.rewards)
            {
                if (row == null || row.level != level) continue;
                row.claimed = 1;
                return true;
            }

            return false;
        }

        private static PlayerPassBallResponseDto CreateZeroState()
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            return new PlayerPassBallResponseDto
            {
                success = true,
                data = new PlayerPassBallDataDto
                {
                    battlePass = new PlayerPassBallDto
                    {
                        id = 0,
                        playerId = string.Empty,
                        level = FirstLevel,
                        experience = 0,
                        created_at = timestamp,
                        updated_at = timestamp
                    },
                    rewards = RebuildRows(null)
                }
            };
        }

        private static List<RewardData> BuildRewardTable()
        {
            var table = new List<RewardData>(Rewards.Length);
            foreach (var reward in Rewards)
                table.Add(new RewardData(reward.Level, reward.Level,
                    JsonUtility.ToJson(new RewardDataJson { type = reward.Type, body = reward.Body })));

            return table;
        }

        /// <summary>
        /// Rebuilds the row list from the table, OR-merging the claimed flag of any persisted
        /// row with the same level. Persisted rows at levels that are not shipped are dropped.
        /// </summary>
        private static List<PlayerRewardDto> RebuildRows(List<PlayerRewardDto> persistedRows)
        {
            var claimedLevels = new HashSet<int>();
            if (persistedRows != null)
            {
                foreach (var persisted in persistedRows)
                {
                    if (persisted != null && persisted.claimed == 1) claimedLevels.Add(persisted.level);
                }
            }

            var rows = new List<PlayerRewardDto>(Rewards.Length);
            foreach (var reward in Rewards)
            {
                rows.Add(new PlayerRewardDto
                {
                    id = reward.Level,
                    playerId = string.Empty,
                    rewardId = reward.Level,
                    awarded_at = string.Empty,
                    claimed = claimedLevels.Contains(reward.Level) ? 1 : 0,
                    level = reward.Level,
                    reward = new RewardDetail { type = reward.Type, body = reward.Body }
                });
            }

            return rows;
        }
    }
}
