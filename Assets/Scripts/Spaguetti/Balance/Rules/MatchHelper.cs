using Domain.Services;

namespace Spaguetti.Balance.Rules
{
    /// <summary>
    /// Kept as the rules' entry point; the comparison itself lives in <see cref="MatchPolicy"/>
    /// so the rival's policy and the board rules can never disagree about what matches.
    /// </summary>
    public static class MatchHelper
    {
        public static bool Matches(string idA, string idB)
        {
            return MatchPolicy.Matches(idA, idB);
        }
    }
}
