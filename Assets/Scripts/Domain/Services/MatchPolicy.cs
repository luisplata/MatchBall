namespace Domain.Services
{
    /// <summary>
    /// Whether two ball ids combine. This is the single source of truth for match semantics:
    /// the rules under <c>Spaguetti.Balance.Rules</c> delegate here, and the rival's policy reads
    /// the same function instead of re-deriving it.
    /// </summary>
    public static class MatchPolicy
    {
        /// <summary>Joker and lead balls carry this id and combine with anything.</summary>
        public const string Wildcard = "all";

        public static bool Matches(string idA, string idB)
        {
            if (idA == null || idB == null) return false;
            if (idA == Wildcard || idB == Wildcard) return true;
            if (idA == idB) return true;
            if (idA.Contains(idB)) return true;
            if (idB.Contains(idA)) return true;
            return false;
        }
    }
}
