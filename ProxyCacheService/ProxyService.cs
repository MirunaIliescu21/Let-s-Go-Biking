using ProxyCacheService.Caching;
using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Caching;
using System.Security.AccessControl;
using System.Web;


namespace ProxyCacheService
{
    /// <summary>
    /// High-level proxy service that provides cached HTTP GET responses and
    /// specialized helpers for JCDecaux endpoints.
    /// </summary>
    public class ProxyService : IProxyService
    {
        /// <summary>
        /// Generic cache for JCDecaux contracts resource. Default factory expects a ctor(string) on the resource type.
        /// </summary>
        private static readonly GenericProxyCache<JcdecauxContractsResource> _contractsCache =
            new GenericProxyCache<JcdecauxContractsResource>();

        /// <summary>
        /// Generic cache for JCDecaux stations resource. Uses keys of the form "jc:stations:{contract}".
        /// </summary>
        private static readonly GenericProxyCache<JcdecauxStationsResource> _stationsCache =
            new GenericProxyCache<JcdecauxStationsResource>();

        // Simple counters for diagnostics (thread-safe via Interlocked)
        private static long _hits = 0;
        private static long _misses = 0;

        /// <summary>
        /// Get JCDecaux contracts payload from the generic cache. TTL default is 3600 seconds (1 hour).
        /// Returns the raw response body as string.
        /// </summary>
        public string GetJcdecauxContractsGeneric(int ttlSeconds = 3600)
        {
            var item = _contractsCache.Get("jc:contracts", ttlSeconds);
            return item.Content;
        }

        /// <summary>
        /// Get JCDecaux stations payload for the specified contract from the generic cache.
        /// TTL default is 30 seconds. Contract name is normalized (trim + lower-case).
        /// </summary>
        public string GetJcdecauxStationsGeneric(string contract, int ttlSeconds = 30)
        {
            var key = $"jc:stations:{contract?.Trim().ToLowerInvariant()}";
            var item = _stationsCache.Get(key, ttlSeconds);
            return item.Content;
        }

        /// <summary>
        /// Ensures the JCDecaux API key is present on requests to api.jcdecaux.com.
        /// If the URL already contains an apiKey parameter, it is left untouched.
        /// This is a convenience helper used before performing an upstream GET.
        /// </summary>
        private static string AddJcdxKeyIfMissing(string url)
        {
            if (!url.Contains("api.jcdecaux.com")) return url;
            if (url.IndexOf("apiKey=", StringComparison.OrdinalIgnoreCase) >= 0) return url;

            var k = ConfigurationManager.AppSettings["JCDECAUX_API_KEY"] ?? "";
            if (string.IsNullOrEmpty(k)) return url; // don't break if missing

            var sep = url.Contains("?") ? "&" : "?";
            return url + sep + "apiKey=" + Uri.EscapeDataString(k);
        }

        /// <summary>
        /// Internal helper that implements a cached GET with TTL, optional force-refresh and optional TTL extension.
        /// Behavior:
        /// - If a non-empty cached HttpGetResource exists and forceRefresh is false => return cached (HIT).
        /// - Otherwise, perform an HTTP GET (HttpGetResource) and cache only on successful HTTP 200 responses.
        /// - Optionally extend TTL on hits if extendTtl is true.
        /// </summary>
        private HttpGetResource GetOrCreate(string rawUrl, DateTimeOffset expiration, bool forceRefresh = false, bool extendTtl = false)
        {
            // normalize URL (inject JCDecaux key if needed)
            var url = AddJcdxKeyIfMissing(rawUrl);

            var cache = MemoryCache.Default;
            var key = $"GET::{url}";

            // 1) Try to read from cache (fast path)
            var existing = cache.Get(key) as HttpGetResource;
            if (!forceRefresh && existing != null && !string.IsNullOrEmpty(existing.Content))
            {
                if (extendTtl) cache.Set(key, existing, expiration);

                System.Threading.Interlocked.Increment(ref _hits);  // increment HIT counter

                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] URL HIT  key='{key}' age={(int)(DateTime.UtcNow - existing.CreatedUtc).TotalSeconds}s");
                return existing;
            }

            // 2) MISS -> fetch from upstream. HttpGetResource handles ORS bearer token and status.
            HttpGetResource created;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] URL MISS key='{key}' fetching…");
                created = new HttpGetResource(url);
            }
            catch (Exception ex)
            {
                // On unexpected exceptions, create an error result. HttpGetResource sets StatusCode = -1 for local errors.
                created = new HttpGetResource(url, $"(Error while fetching: {ex.Message})");
            }

            // 3) Only cache successful HTTP 200 responses with non-empty body
            if (created.StatusCode == 200 && !string.IsNullOrEmpty(created.Content))
            {
                cache.Set(key, created, expiration);
                System.Threading.Interlocked.Increment(ref _misses);  // count real MISS that resulted in caching
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] URL MISS->CACHED key='{key}' ttl={(int)(expiration - DateTimeOffset.UtcNow).TotalSeconds}s len={created.Content.Length}");

            } else
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] URL MISS_NO_CACHE key='{key}' status={created.StatusCode}");
            }

            return created;
        }


        /// <summary>
        /// Convenience: perform a GET with no expiration (infinite) and return the body content.
        /// </summary>
        public string Get(string url)
        {
            var res = GetOrCreate(url, ObjectCache.InfiniteAbsoluteExpiration, forceRefresh: false);
            return res.Content;
        }

        /// <summary>
        /// Perform a GET with a specific TTL (in seconds). If ttlSeconds &lt;= 0, a default of 60s is used.
        /// </summary>
        public string GetWithTtl(string url, double ttlSeconds, bool forceRefresh = false, bool extendTtl = false)
        {
            if (ttlSeconds <= 0) ttlSeconds = 60;
            var expiration = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);

            var res = GetOrCreate(url, expiration, forceRefresh: forceRefresh, extendTtl: extendTtl);
            return res.Content;
        }

        /// <summary>
        /// Returns a small JSON blob describing cache metadata for the requested URL (HIT/MISS, key, length, age).
        /// This is useful for health checks and debugging cache behavior.
        /// </summary>
        public string GetWithMeta(string url, double ttlSeconds)
        {
            if (ttlSeconds <= 0) ttlSeconds = 60;
            var expiration = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);

            var normalized = AddJcdxKeyIfMissing(url);
            var key = $"GET::{normalized}";
            var cache = MemoryCache.Default;

            var hit = cache.Get(key) as HttpGetResource;
            if (hit != null && !string.IsNullOrEmpty(hit.Content))
            {
                var age = (int)Math.Max(0, (DateTime.UtcNow - hit.CreatedUtc).TotalSeconds);
                System.Threading.Interlocked.Increment(ref _hits); // count HIT
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] META HIT key='{key}' age={age}s");
                return $"{{\"cache\":\"HIT\",\"key\":\"{Escape(key)}\",\"ageSeconds\":{age},\"length\":{hit.Content.Length}}}";
            }

            var res = GetOrCreate(url, expiration, forceRefresh: false, extendTtl: false);
            var cachedNow = cache.Get(key) as HttpGetResource;
            var cachedLabel = (cachedNow != null) ? "MISS->CACHED" : "MISS_NO_CACHE";
            var len = res.Content == null ? 0 : res.Content.Length;
            return $"{{\"cache\":\"{cachedLabel}\",\"key\":\"{Escape(key)}\",\"length\":{len}}}";
        }

        private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";

        /// <summary>
        /// Evict a specific absolute URL from the internal memory cache.
        /// The URL will be normalized (JCDecaux API key may be injected) before eviction.
        /// </summary>
        public void Evict(string url)
        {
            MemoryCache.Default.Remove($"GET::{AddJcdxKeyIfMissing(url)}");
        }

        /// <summary>
        /// Evict a generic cache key (used by GenericProxyCache entries such as "jc:contracts").
        /// </summary>
        public void EvictGeneric(string key)
        {
            // for generic key "jc:contracts" / "jc:stations:lyon"
            MemoryCache.Default.Remove(key);
        }

        /// <summary>
        /// Return simple cache statistics as a JSON string. "hits" and "misses" are counters since process start.
        /// </summary>
        public string Status()
        {
            // items is the current number of cached objects, not "how many URLs have I ever called".
            var items = MemoryCache.Default.GetCount();
            var hits = System.Threading.Interlocked.Read(ref _hits);
            var misses = System.Threading.Interlocked.Read(ref _misses);
            return $"{{\"hits\":{hits},\"misses\":{misses},\"items\":{items}}}";
        }

    }
}