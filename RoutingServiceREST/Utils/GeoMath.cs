namespace RoutingServiceREST.Utils
{
    public static class GeoMath
    {
        public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * System.Math.PI / 180.0;
            double dLon = (lon2 - lon1) * System.Math.PI / 180.0;
            double a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2) +
            System.Math.Cos(lat1 * System.Math.PI / 180.0) * System.Math.Cos(lat2 * System.Math.PI / 180.0) *
            System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
            double c = 2 * System.Math.Atan2(System.Math.Sqrt(a), System.Math.Sqrt(1 - a));
            return R * c;
        }


        public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        => HaversineKm(lat1, lon1, lat2, lon2) * 1000.0;
    }
}