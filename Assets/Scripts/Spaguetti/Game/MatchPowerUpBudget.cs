using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spaguetti.Game
{
    /// <summary>
    /// One player's match-local power-up budget: seeded from the account selection at match boot,
    /// decremented in memory on use, and thrown away with the match.
    ///
    /// The constructor DEEP-COPYs the seed on purpose. <c>IPowerUpsUseCase.GetSelectedPowerUps()</c>
    /// returns a new list holding the same <see cref="PowerUpData"/> instances the use case caches, so
    /// mutating <c>quantity</c> through the seed would compile and silently corrupt the account
    /// inventory for every later match (PvE included).
    ///
    /// One instance per player, owned privately by that player's mediator: there is no shared number
    /// left that two boards could draw from twice.
    /// </summary>
    public sealed class MatchPowerUpBudget
    {
        private readonly List<PowerUpData> _quantity;
        private readonly string _owner;

        public MatchPowerUpBudget(IReadOnlyList<PowerUpData> seed, string owner = "player")
        {
            _owner = owner;
            _quantity = new List<PowerUpData>();
            if (seed == null) return;

            foreach (var powerUp in seed)
            {
                if (powerUp == null) continue;
                // A new instance, never the use case's: see the class summary.
                _quantity.Add(new PowerUpData
                {
                    id = powerUp.id,
                    selected = true,
                    quantity = powerUp.quantity
                });
            }
        }

        /// <summary>
        /// What this player may still spawn, given the orbs of each type already on THEIR OWN board.
        /// The caller passes its own zone's counter: reservations are never aggregated across boards.
        /// Types with nothing left are dropped, which is what keeps an exhausted type from spawning.
        /// </summary>
        public IReadOnlyList<PowerUpData> Available(Func<string, int> reservedOnOwnBoard)
        {
            var available = new List<PowerUpData>(_quantity.Count);
            foreach (var entry in _quantity)
            {
                var reserved = reservedOnOwnBoard != null ? reservedOnOwnBoard(entry.id) : 0;
                var quantity = entry.quantity - reserved;
                // The designated per-spawn tripwire: there are no tests, so this log is the check.
                Debug.Log($"[PowerUpBudget:{_owner}] '{entry.id}' q={entry.quantity} reserved={reserved} available={quantity}.");
                if (quantity <= 0) continue;
                available.Add(new PowerUpData { id = entry.id, selected = true, quantity = quantity });
            }

            return available;
        }

        /// <summary>Charges one unit of <paramref name="id"/>. Memory only, never below zero.</summary>
        public void Consume(string id)
        {
            var entry = _quantity.FirstOrDefault(powerUp => powerUp.id == id);
            if (entry == null)
            {
                // An id that is not in the seed cannot be charged: visible instead of silent.
                Debug.LogWarning($"[PowerUpBudget:{_owner}] unknown power-up id '{id}'; nothing charged.");
                return;
            }

            if (entry.quantity <= 0)
            {
                Debug.Log($"[PowerUpBudget:{_owner}] '{id}' is already exhausted; nothing charged.");
                return;
            }

            entry.quantity--;
            Debug.Log($"[PowerUpBudget:{_owner}] consumed '{id}'; {entry.quantity} left.");
        }

        public bool IsEmpty => _quantity.All(powerUp => powerUp.quantity <= 0);

        /// <summary>Remaining quantities, for the boot log that identifies this budget.</summary>
        public string Describe()
        {
            return _quantity.Count == 0
                ? "(empty seed)"
                : string.Join(", ", _quantity.Select(powerUp => $"{powerUp.id}={powerUp.quantity}"));
        }
    }
}
