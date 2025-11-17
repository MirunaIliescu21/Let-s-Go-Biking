using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace ProxyCacheService
{
    [ServiceContract]
    public interface IProxyService
    {

        /// <summary>
        /// Return the raw JSON payload for the JCDecaux "contracts" endpoint.
        /// Implementations typically use an internal generic cache (GenericProxyCache)
        /// and honor the provided TTL (seconds). 
        /// This method returns the response body as a string.
        /// </summary>
        [OperationContract]
        string GetJcdecauxContractsGeneric(int ttlSeconds);


        /// <summary>
        /// Return the raw JSON payload for the JCDecaux "stations" endpoint
        /// for the given contract. The contract name is normalized by the
        /// implementation (trim + lower-case) and the response is typically cached.
        /// The TTL is specified in seconds.
        /// </summary>
        [OperationContract]
        string GetJcdecauxStationsGeneric(string contract, int ttlSeconds);


        /// <summary>
        /// Perform a plain HTTP GET for the specified absolute URL and return
        /// the response body as a string. The implementation injects API keys
        /// or add ORS bearer tokens for known hosts before fetching.
        /// This convenience method uses the internal URL-based cache with an
        /// infinite/default expiration when called.
        /// </summary>
        [OperationContract]
        string Get(string url);


        /// <summary>
        /// Perform a GET and use an explicit TTL (seconds) for caching the result.
        /// Behavior:
        ///  If a non-empty cached entry exists and <paramref name="forceRefresh"/> is false, the cached value is returned.
        ///  Otherwise an upstream GET is performed and only successful HTTP 200 responses with a non-empty body are cached.
        ///  If <paramref name="extendTtl"/> is true and a cached entry is returned, the TTL is extended.
        /// Parameters:
        ///   <paramref name="url"/> - absolute URL to fetch
        ///   <paramref name="ttlSeconds"/> - desired cache TTL in seconds
        ///   <paramref name="forceRefresh"/> - when true, bypass the cache and fetch upstream
        ///   <paramref name="extendTtl"/> - when true, extend the TTL on cache hits
        /// Returns the response body as a string.
        /// </summary>
        [OperationContract]
        string GetWithTtl(string url, double ttlSeconds, bool forceRefresh, bool extendTtl);


        /// <summary>
        /// Evict the cached entry for the provided absolute URL.
        /// The implementation normalizes the URL (by injecting the JCDecaux apiKey)
        /// before removing the corresponding cache item.
        /// </summary>
        [OperationContract]
        void Evict(string url);


        /// <summary>
        /// Return a small JSON describing cache metadata for the requested URL.
        /// The JSON typically contains fields like "cache" (HIT/MISS), "key", "ageSeconds" and "length".
        /// Calling this method may trigger a fetch if the item is not present in the cache.
        /// </summary>
        [OperationContract]
        string GetWithMeta(string url, double ttlSeconds);

        /// <summary>
        /// Evict a generic cache key used by the generic caches (for example "jc:contracts"
        /// or "jc:stations:{contract}"). This is used to invalidate entries created by
        /// GenericProxyCache instances.
        /// </summary>
        [OperationContract]
        void EvictGeneric(string key);

        /// <summary>
        /// Return simple runtime statistics about the proxy cache as a JSON string.
        /// Typical keys include "hits", "misses" and "items" (= current number of cached entries).
        /// </summary>
        [OperationContract]
        string Status();
    }
}

