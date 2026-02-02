using System.Collections.Generic;
using Spaguetti.Balance.Rules;

namespace Spaguetti.Balance
{
    public static class DefaultRulesFactory
    {
        public static IEnumerable<ICombinationRule> Create(IMusicController music, IDelayProvider delay)
        {
            // Keep same order as the original implementation
            return new ICombinationRule[]
            {
                // new Rules.VerticalPairRule(true, music, delay), // left
                // new Rules.VerticalPairRule(false, music, delay), // right
                new LOrNRule(music, delay),
                new HorizontalPairRule(music, delay),
                new HimselfRule(music, delay)
            };
        }
    }
}