using System.Configuration;
using System.Net.Http;
using System;

namespace ProxyCacheService.Caching
{
    /// <summary>
    /// Represents the JCDecaux stations resource for a specific contract.
    /// This type is designed to be used inside the <see cref="GenericProxyCache{T}"/> pattern.
    /// 
    /// The constructor expects a cache key in the form of "jc:stations:{contract}".
    /// Upon instantiation, it extracts the contract name from the key and performs a
    /// blocking HTTP GET request to fetch the station list for that JCDecaux contract.
    /// 
    /// The resulting JSON string is stored in the read-only <see cref="Content"/> property.
    /// </summary>
    public sealed class JcdecauxStationsResource
    {
        /// <summary>
        /// The raw JSON content returned by the JCDecaux API for the given contract.
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Creates a new instance of <see cref="JcdecauxStationsResource"/> using a cache key.
        /// Expected key format: <c>jc:stations:{contract}</c>.
        /// The constructor extracts the contract name and performs an HTTP GET to
        /// <c>https://api.jcdecaux.com/vls/v3/stations?contract={contract}&amp;apiKey={apiKey}</c>.
        /// </summary>
        public JcdecauxStationsResource(string cacheKey)
        {
            var parts = cacheKey.Split(':');
            var contract = parts.Length >= 3 ? parts[2] : throw new ArgumentException("Invalid key");
            var apiKey = ConfigurationManager.AppSettings["JCDECAUX_API_KEY"];
            var url = $"https://api.jcdecaux.com/vls/v3/stations?contract={contract}&apiKey={apiKey}";
            using (var http = new HttpClient())
            {
                Content = http.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }
    }
}
