namespace RoutingServiceREST.Services
{
    public readonly struct LatLon
    {
        public readonly double Lat;
        public readonly double Lon;
        public LatLon(double lat, double lon) { Lat = lat; Lon = lon; }
    }


    public interface IGeocodingService
    {
        bool TryGeocode(string text, out LatLon coord);
    }
}