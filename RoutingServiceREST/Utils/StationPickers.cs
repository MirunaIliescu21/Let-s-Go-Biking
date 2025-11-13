using System.Collections.Generic;
using System.Linq;


namespace RoutingServiceREST.Utils
{
    public static class StationPickers
    {
        public static JcdecauxStation Closest(IEnumerable<JcdecauxStation> stations, double lat, double lon)
        => stations.OrderBy(s => GeoMath.HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon)).FirstOrDefault();


        public static JcdecauxStation ClosestWithBikes(IEnumerable<JcdecauxStation> stations, double lat, double lon)
        => stations.Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
        .OrderBy(s => GeoMath.HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon)).FirstOrDefault();


        public static JcdecauxStation ClosestWithStands(IEnumerable<JcdecauxStation> stations, double lat, double lon)
        => stations.Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
        .OrderBy(s => GeoMath.HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon)).FirstOrDefault();


        public static JcdecauxStation WithBikesNearestTo(IEnumerable<JcdecauxStation> stations, double targetLat, double targetLon)
        => stations.Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
        .OrderBy(s => GeoMath.HaversineKm(targetLat, targetLon, s.Position.Lat, s.Position.Lon)).FirstOrDefault();


        public static JcdecauxStation WithStandsNearestTo(IEnumerable<JcdecauxStation> stations, double targetLat, double targetLon)
        => stations.Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
        .OrderBy(s => GeoMath.HaversineKm(targetLat, targetLon, s.Position.Lat, s.Position.Lon)).FirstOrDefault();
    }
}