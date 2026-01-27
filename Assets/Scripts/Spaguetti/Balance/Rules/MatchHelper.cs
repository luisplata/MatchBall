namespace Spaguetti.Balance.Rules
{
    public static class MatchHelper
    {
        public static bool Matches(string idA, string idB)
        {
            if (idA == null || idB == null) return false;
            if (idA == "all" || idB == "all") return true;
            if (idA == idB) return true;
            if (idA.Contains(idB)) return true;
            if (idB.Contains(idA)) return true;
            return false;
        }
    }
}
