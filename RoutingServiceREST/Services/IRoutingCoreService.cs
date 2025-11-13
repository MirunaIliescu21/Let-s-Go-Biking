using System.Collections.Generic;


namespace RoutingServiceREST.Services
{
    public sealed class OrsRoute
    {
        public double DurationSec { get; set; }
        public double DistanceMeters { get; set; }
        public List<double[]> Coords { get; set; } = new List<double[]>(); // [lat, lon]
    }


    public interface IRoutingCoreService
    {
        OrsRoute GetRoute(string profile, LatLon from, LatLon to);
    }
}
