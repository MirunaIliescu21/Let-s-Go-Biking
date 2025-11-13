using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace RoutingServiceREST.Services
{
    public sealed class JcdecauxStationRepository : IStationRepository
    {
        private readonly IProxyFactory _factory;
        public JcdecauxStationRepository(IProxyFactory factory) => _factory = factory;

        private const int TTL_CONTRACTS_SECONDS = 3600; // 1h
        private const int TTL_STATIONS_SECONDS = 30;   // 30s (availability changes often)
        public List<JcdecauxStation> GetAllStations()
        {
            using (var proxy = _factory.Create())
            {
                string url = "https://api.jcdecaux.com/vls/v3/stations";
                string json = proxy.GetWithTtl(url, TTL_STATIONS_SECONDS, false, false);
                var trimmed = json?.TrimStart() ?? "";
                if (string.IsNullOrEmpty(trimmed))
                    throw new Exception("JCDecaux empty response.");
                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                    throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));
                return JsonConvert.DeserializeObject<List<JcdecauxStation>>(json);
            }
        }


        // Bonus passthroughs to keep your public endpoints unchanged
        public List<JcDecauxContract> GetContracts()
        {
            using (var proxy = _factory.Create())
            {
                string url = "https://api.jcdecaux.com/vls/v3/contracts";
                string json = proxy.GetWithTtl(url, TTL_CONTRACTS_SECONDS, false, false);
                var trimmed = json?.TrimStart() ?? "";
                if (string.IsNullOrEmpty(trimmed))
                    throw new Exception("JCDecaux empty response.");
                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                    throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));
                var contracts = JsonConvert.DeserializeObject<List<JcDecauxContract>>(json) ?? new List<JcDecauxContract>();
                foreach (var c in contracts) c.Cities = c.Cities ?? new List<string>();
                return contracts;
            }
        }


        public List<JcdecauxStation> GetStations(string contract)
        {
            using (var proxy = _factory.Create())
            {
                string baseUrl = "https://api.jcdecaux.com/vls/v3/stations";
                string url = string.IsNullOrWhiteSpace(contract) ? baseUrl : $"{baseUrl}?contract={Uri.EscapeDataString(contract)}";
                string json = proxy.GetWithTtl(url, TTL_STATIONS_SECONDS, false, false);
                var trimmed = json?.TrimStart() ?? "";
                if (string.IsNullOrEmpty(trimmed))
                    throw new Exception("JCDecaux empty response.");
                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
                    throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));
                return JsonConvert.DeserializeObject<List<JcdecauxStation>>(json);
            }
        }
    }
}