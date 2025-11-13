using System;
using Newtonsoft.Json;


namespace RoutingServiceREST.Services
{
    public sealed class OrsGeocodingService : IGeocodingService
    {
        private readonly IProxyFactory _factory;
        private const int TTL_GEOCODE_SECONDS = 86400; // 24h
        public OrsGeocodingService(IProxyFactory factory) => _factory = factory;


        public bool TryGeocode(string text, out LatLon coord)
        {
            coord = new LatLon(0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;


            using (var proxy = _factory.Create())
            {
                string baseUrl = "https://api.openrouteservice.org/geocode/search";
                bool cityMode = Utils.QueryHeuristics.IsLikelyCityQuery(text);
                string url = $"{baseUrl}?text={Uri.EscapeDataString(text)}&size=1" +
                (cityMode ? "&layers=locality,localadmin,county,region,macroregion" : "");


                string json = proxy.GetWithTtl(url, TTL_GEOCODE_SECONDS, false, false);
                var trimmed = json?.TrimStart() ?? "";
                if (string.IsNullOrEmpty(trimmed)) return false;
                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                    throw new Exception("ORS geocode non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));


                dynamic doc = JsonConvert.DeserializeObject(json);
                if (doc?.features == null || doc.features.Count == 0)
                {
                    if (cityMode)
                    {
                        url = $"{baseUrl}?text={Uri.EscapeDataString(text)}&size=1";
                        json = proxy.GetWithTtl(url, TTL_GEOCODE_SECONDS, false, false);
                        trimmed = json?.TrimStart() ?? "";
                        if (string.IsNullOrEmpty(trimmed)) return false;
                        if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                            throw new Exception("ORS geocode non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));
                        doc = JsonConvert.DeserializeObject(json);
                    }
                    if (doc?.features == null || doc.features.Count == 0) return false;
                }


                double dlon = (double)doc.features[0].geometry.coordinates[0];
                double dlat = (double)doc.features[0].geometry.coordinates[1];
                coord = new LatLon(dlat, dlon);
                return true;
            }
        }
    }
}
