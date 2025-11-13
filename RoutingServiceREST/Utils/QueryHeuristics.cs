using System;
using System.Linq;


namespace RoutingServiceREST.Utils
{
    public static class QueryHeuristics
    {
        public static bool IsLikelyCityQuery(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (s.IndexOf(',') >= 0) return false;
            if (s.Any(char.IsDigit)) return false;
            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 3;
        }
    }
}
