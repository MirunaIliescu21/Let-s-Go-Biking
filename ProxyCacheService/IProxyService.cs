using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace ProxyCacheService
{
    [ServiceContract]
    public interface IProxyService
    {
        [OperationContract]
        string GetJcdecauxContractsGeneric(int ttlSeconds);

        [OperationContract]
        string GetJcdecauxStationsGeneric(string contract, int ttlSeconds);

        // MVP: doar GET; mai târziu poți adăuga POST/headers.
        [OperationContract]
        string Get(string url);

        // Overload cu TTL (secunde) pentru cache
        [OperationContract]
        string GetWithTtl(string url, double ttlSeconds, bool forceRefresh, bool extendTtl);

        [OperationContract]
        void Evict(string url);

        // Operations for demo and control
        [OperationContract]
        string GetWithMeta(string url, double ttlSeconds);

        [OperationContract]
        void EvictGeneric(string key);

        [OperationContract]
        string Stats();
    }
}

