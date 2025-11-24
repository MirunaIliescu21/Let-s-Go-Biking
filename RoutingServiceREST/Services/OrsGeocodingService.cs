using System;
using Newtonsoft.Json;


namespace RoutingServiceREST.Services
{
    /// <summary>
    /// Provides geocoding through the OpenRouteService API, using the SOAP ProxyCacheService
    /// for all HTTP requests and for transparent caching of results.
    ///
    /// Key responsibilities:
    /// Converts free-text locations (e.g., "Lyon", "Place Bellecour") into coordinates.
    /// Applies query heuristics: detects probable city queries and adjusts ORS parameters
    ///   (country restriction, locality-only search) to avoid ambiguous or incorrect matches.
    /// Builds ORS geocoding URLs and delegates execution to the ProxyServiceClient,
    ///   benefiting from a 24h TTL cache to reduce API traffic.
    /// Validates responses and safely handles non-JSON results (HTML error pages, rate limits).
    /// Extracts the first GeoJSON feature returned by ORS and maps it to a Lat/Lon pair.
    ///
    /// This service isolates the routing core from ORS HTTP details and ensures robust,
    /// consistent, and cached geocoding across the entire routing pipeline.
    /// </summary>
    public sealed class OrsGeocodingService : IGeocodingService
    {
        private readonly IProxyFactory _factory;
        private const int TTL_GEOCODE_SECONDS = 86400; // 24h
        public OrsGeocodingService(IProxyFactory factory) => _factory = factory;

        private static string DetectCountryCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var lower = text.ToLowerInvariant();

            // Belgium
            if (lower.Contains("belgium") || lower.Contains("belgique") ||
                lower.Contains("brussels") || lower.Contains("bruxelles"))
                return "BE";

            // France
            if (lower.Contains("france") || lower.Contains("paris") ||
                lower.Contains("lyon") || lower.Contains("toulouse") ||
                lower.Contains("marseille"))
                return "FR";

            // Spain
            if (lower.Contains("spain") || lower.Contains("españa") ||
                lower.Contains("madrid") || lower.Contains("barcelona") ||
                lower.Contains("valencia") || lower.Contains("sevilla"))
                return "ES";

            // Ireland
            if (lower.Contains("ireland") || lower.Contains("irlande") ||
                lower.Contains("dublin"))
                return "IE";

            // Luxembourg
            if (lower.Contains("luxembourg"))
                return "LU";

            return null;
        }

        public bool TryGeocode(string text, out LatLon coord)
        {
            coord = new LatLon(0, 0);
            if (string.IsNullOrWhiteSpace(text))
                return false;


            using (var proxy = _factory.Create())
            {
                string baseUrl = "https://api.openrouteservice.org/geocode/search";
                bool cityMode = Utils.QueryHeuristics.IsLikelyCityQuery(text);

                string countryCode = DetectCountryCode(text);
                string countryParam = countryCode != null
                    ? $"&boundary.country={countryCode}"
                    : "";

                string url = $"{baseUrl}?text={Uri.EscapeDataString(text)}&size=1" +
                                 countryParam +
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
