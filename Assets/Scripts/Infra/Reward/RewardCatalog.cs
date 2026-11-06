using System;
using System.Collections.Generic;
using App.Reward;
using Cysharp.Threading.Tasks;
using Domain.Entities;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Infra.Reward
{
    /// <summary>
    /// Dictionary-backed reward registry. Construction is strict (empty, blank or duplicate type
    /// ids throw), while runtime lookups are tolerant: an unregistered persisted type can never
    /// break the pass, it is only skipped and logged.
    /// </summary>
    public sealed class RewardCatalog : IRewardCatalog
    {
        private readonly Dictionary<string, IRewardHandler> _handlers;

        public RewardCatalog(IEnumerable<IRewardHandler> handlers)
        {
            if (handlers == null) throw new ArgumentNullException(nameof(handlers));

            var map = new Dictionary<string, IRewardHandler>(StringComparer.Ordinal);
            foreach (var handler in handlers)
            {
                if (handler == null)
                    throw new ArgumentException("The reward catalog received a null handler.", nameof(handlers));

                if (string.IsNullOrWhiteSpace(handler.TypeId))
                    throw new ArgumentException("A reward handler has an empty TypeId.", nameof(handlers));

                if (!map.TryAdd(handler.TypeId, handler))
                    throw new ArgumentException($"Duplicate reward handler for type '{handler.TypeId}'.",
                        nameof(handlers));
            }

            if (map.Count == 0)
                throw new ArgumentException("The reward catalog needs at least one handler.", nameof(handlers));

            _handlers = map;
        }

        public bool Supports(string typeId)
        {
            return !string.IsNullOrWhiteSpace(typeId) && _handlers.ContainsKey(typeId);
        }

        public RewardDisplayData Describe(string typeId, string body)
        {
            if (!Supports(typeId))
            {
                Debug.LogWarning($"[RewardCatalog] No handler registered for reward type '{typeId}'; using the neutral label.");
                return new RewardDisplayData(typeId ?? string.Empty, typeId ?? string.Empty);
            }

            return _handlers[typeId].Describe(body);
        }

        public async UniTask<RewardGrantOutcome> GrantAsync(string typeId, string body)
        {
            if (!Supports(typeId))
            {
                Debug.LogError($"[RewardCatalog] No handler registered for reward type '{typeId}'; grant skipped.");
                return RewardGrantOutcome.Skipped;
            }

            try
            {
                return await _handlers[typeId].GrantAsync(body);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[RewardCatalog] Grant for reward type '{typeId}' failed: {exception}");
                return RewardGrantOutcome.Failed;
            }
        }

        /// <summary>
        /// Checks a shipped table against the registered handlers. Returns one message per problem:
        /// empty table, null row, duplicate level, unreadable payload, unknown type, empty body.
        /// </summary>
        public IReadOnlyList<string> ValidateTable(IEnumerable<RewardData> rewards)
        {
            var issues = new List<string>();
            if (rewards == null)
            {
                issues.Add("The ball pass reward table is null.");
                return issues;
            }

            var levels = new HashSet<int>();
            var count = 0;
            foreach (var reward in rewards)
            {
                if (reward == null)
                {
                    issues.Add("The ball pass reward table contains a null row.");
                    continue;
                }

                count++;

                if (!levels.Add(reward.Level))
                    issues.Add($"Level {reward.Level} appears more than once.");

                if (!TryReadRow(reward, out var typeId, out var body))
                {
                    issues.Add($"Level {reward.Level} has an unreadable reward payload.");
                    continue;
                }

                if (!Supports(typeId))
                    issues.Add($"Level {reward.Level} uses '{typeId}', which has no registered handler.");

                if (string.IsNullOrWhiteSpace(body))
                    issues.Add($"Level {reward.Level} has an empty reward body.");
            }

            if (count == 0) issues.Add("The ball pass reward table is empty.");
            return issues;
        }

        private static bool TryReadRow(RewardData reward, out string typeId, out string body)
        {
            typeId = null;
            body = null;
            if (string.IsNullOrWhiteSpace(reward.Reward)) return false;

            try
            {
                var payload = JObject.Parse(reward.Reward);
                typeId = payload.Value<string>("type");
                body = payload.Value<string>("body");
            }
            catch (Exception)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(typeId);
        }
    }
}
