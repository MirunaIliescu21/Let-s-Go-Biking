using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection.Emit;

namespace RoutingServiceREST.Planning
{
    /// <summary>
    /// StationContext packages all the JCDecaux information needed by the planner:
    /// the full list of stations, the origin contract and the destination contract.
    /// It keeps the planning logic clean and decoupled from the REST layer.
    /// </summary>
    public sealed class StationContext
    {
        public IReadOnlyList<JcdecauxStation> All { get; set; } = new List<JcdecauxStation>();
        public string OriginContract { get; set; }
        public string DestContract { get; set; }
    }
}
