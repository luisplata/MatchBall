using System.Collections.Generic;

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
                new Rules.HorizontalPairRule(music, delay),
                new Rules.HimselfRule(music, delay)
            };
        }
    }
}