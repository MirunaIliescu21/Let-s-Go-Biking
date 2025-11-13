using System;
using System.Runtime.Caching;

namespace ProxyCacheService.Caching
{
    /// <summary>
    /// Small generic cache wrapper that stores instances of T in MemoryCache keyed by a string.
    /// The default behavior expects T to have a constructor that accepts a single string key.
    /// For more control you can provide a factory function via the constructor.
    /// </summary>
    public class GenericProxyCache<T> where T : class
    {
        private readonly MemoryCache _cache = MemoryCache.Default;
        public DateTimeOffset dt_default;

        /// <summary>
        /// Constructs a new GenericProxyCache with infinite default expiration.
        /// </summary>
        public GenericProxyCache()
        {
            dt_default = ObjectCache.InfiniteAbsoluteExpiration;
        }

        /// <remarks>
        /// Note: Some Get(...) overloads may appear unused within this project,
        /// but they are part of the required public API described in the assignment
        /// ("This class should have 3 getters:"). Keeping them ensures compliance
        /// and provides flexibility for future extensions.
        /// </remarks>

        // Cache getter overloads:
        public T Get(string cacheItemName)
        {
            return GetInternal(cacheItemName, dt_default);
        }

        public T Get(string cacheItemName, double dt_seconds)
        {
            var abs = DateTimeOffset.Now.AddSeconds(dt_seconds);
            return GetInternal(cacheItemName, abs);
        }

        public T Get(string cacheItemName, DateTimeOffset dt)
        {
            return GetInternal(cacheItemName, dt);
        }

        /// <summary>
        /// Tiny generic cache: creates T via new T(key) when absent, stores it with a TTL.
        /// Used to demonstrate a reusable pattern (contracts, stations).
        /// Console debug shows "Cache HIT/MISS for key '<key>'".
        /// </summary>
        private T GetInternal(string key, DateTimeOffset absExpiration)
        {
            if (_cache.Get(key) is T obj)
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] Cache HIT for key '{key}'");
                return obj;
            }

            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:T}] Cache MISS for key '{key}'");

            obj = Activator.CreateInstance(typeof(T), key) as T
                  ?? throw new InvalidOperationException(
                         $"Type {typeof(T).FullName} must have a public constructor .ctor(string).");

            _cache.Set(key, obj, absExpiration);
            return obj;
        }
    }
}

