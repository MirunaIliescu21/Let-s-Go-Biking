using System.Collections.Generic;

namespace RoutingServiceREST.Planning
{
    public sealed class StationContext
    {
        public IReadOnlyList<JcdecauxStation> All { get; set; } = new List<JcdecauxStation>();
        public string OriginContract { get; set; }
        public string DestContract { get; set; }
    }
}
