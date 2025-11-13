using RoutingServiceREST.ProxyRef;

namespace RoutingServiceREST.Services
{
    public interface IProxyFactory {
        ProxyServiceClient Create();
    }
    public sealed class ProxyFactory : IProxyFactory
    {
        public ProxyServiceClient Create() => new ProxyServiceClient();
    }
}