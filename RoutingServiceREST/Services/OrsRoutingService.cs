using System;
using System.Globalization;
using Newtonsoft.Json;


namespace RoutingServiceREST.Services
{
    public sealed class OrsRoutingService : IRoutingCoreService
    {
        private readonly IProxyFactory _factory;
        private const int TTL_ROUTE_SECONDS = 120; // 2 min
        public OrsRoutingService(IProxyFactory factory) => _factory = factory;


        public OrsRoute GetRoute(string profile, LatLon from, LatLon to)
        {
            using (var proxy = _factory.Create())
            {
                string url =
                $"https://api.openrouteservice.org/v2/directions/{profile}" +
                $"?start={toStr(from.Lon)},{toStr(from.Lat)}&end={toStr(to.Lon)},{toStr(to.Lat)}";


                string json = proxy.GetWithTtl(url, TTL_ROUTE_SECONDS, false, false);
                var trimmed = json?.TrimStart() ?? "";
                if (string.IsNullOrEmpty(trimmed))
                    throw new Exception("ORS directions empty response.");
                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                    throw new Exception("ORS directions non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));


                dynamic doc = JsonConvert.DeserializeObject(json);
                var seg = doc.features[0].properties.segments[0];
                double dur = (double)seg.duration; // seconds
                double dist = (double)seg.distance; // meters


                var coords = new System.Collections.Generic.List<double[]>();
                foreach (var p in doc.features[0].geometry.coordinates)
                {
                    double lon = (double)p[0];
                    double lat = (double)p[1];
                    coords.Add(new[] { lat, lon });
                }
                return new OrsRoute { DurationSec = dur, DistanceMeters = dist, Coords = coords };
            }
        }

        private static string toStr(double v) => v.ToString(CultureInfo.InvariantCulture);
    }
}