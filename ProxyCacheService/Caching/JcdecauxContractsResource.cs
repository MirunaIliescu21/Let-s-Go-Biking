using System.Configuration;
using System.Net.Http;

namespace ProxyCacheService.Caching
{
    /// <summary>
    /// Represents the JCDecaux contracts resource. This type knows how to fetch its own
    /// content given a cache key (the constructor argument is currently unused).
    /// 
    /// This class is designed to be used within the <see cref="GenericProxyCache{T}"/> pattern,
    /// where the constructor is automatically invoked when the cache entry is missing.
    /// </summary>
    public sealed class JcdecauxContractsResource
    {
        public string Content { get; }

        /// <summary>
        /// The constructor performs a synchronous HTTP GET to
        /// <c>https://api.jcdecaux.com/vls/v3/contracts</c>, using the API key from <c>App.config</c>.
        /// The incoming string parameter (cache key) is not used — it exists only to satisfy;
        /// the type constructs the URL from configuration.
        /// </summary>
        public JcdecauxContractsResource(string _ /*unused*/)
        {
            var apiKey = ConfigurationManager.AppSettings["JCDECAUX_API_KEY"];
            var url = $"https://api.jcdecaux.com/vls/v3/contracts?apiKey={apiKey}";
            using (var http = new HttpClient())
            {
                Content = http.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }
    }
}
