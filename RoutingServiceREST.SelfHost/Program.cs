using System;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using System.Xml;
using RoutingServiceREST;

namespace RoutingServiceREST.SelfHost
{
    internal static class Program
    {
        /// <summary>
        /// Entry point for the self-hosted RoutingServiceREST.
        /// This console app hosts the REST routing service on the same URL
        /// that was previously configured in App.config.
        /// At the same time, it still uses the WCF client configuration in App.config
        /// to call the ProxyCacheService.
        /// </summary>
        static void Main()
        {
            // Base address for the REST service.
            // It must match the address used previously in App.config and in the frontend.
            var baseAddress = new Uri("http://localhost:8733/Design_Time_Addresses/RoutingServiceREST/Service1/");

            // WebHttp binding configuration (equivalent to LargeWebBinding from the original App.config).
            var webBinding = new WebHttpBinding
            {
                Name = "LargeWebBinding",
                MaxReceivedMessageSize = 20000000,
                MaxBufferSize = 20000000,
                MaxBufferPoolSize = 20000000
            };

            // Reader quotas must be assigned via a new XmlDictionaryReaderQuotas instance
            webBinding.ReaderQuotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 64,
                MaxStringContentLength = 20000000,
                MaxArrayLength = 20000000,
                MaxBytesPerRead = 20000000,
                MaxNameTableCharCount = 20000000
            };

            // WebServiceHost is a specialized ServiceHost for REST (WebHttp)
            // that makes it easier to configure WebHttpBehavior.
            using (var host = new WebServiceHost(typeof(RoutingService), baseAddress))
            {
                try
                {
                    // Main REST endpoint exposing IRoutingServiceREST at the base address.
                    var endpoint = host.AddServiceEndpoint(
                        typeof(IRoutingServiceREST),
                        webBinding,
                        "" // empty relative address; exactly baseAddress
                    );

                    // Enable WebHttp behavior so the service understands REST/JSON requests.
                    var webBehavior = new WebHttpBehavior
                    {
                        HelpEnabled = true,
                        DefaultOutgoingResponseFormat = WebMessageFormat.Json
                    };
                    endpoint.EndpointBehaviors.Add(webBehavior);

                    var debugBehavior = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                    if (debugBehavior == null)
                    {
                        debugBehavior = new ServiceDebugBehavior();
                        host.Description.Behaviors.Add(debugBehavior);
                    }
                    debugBehavior.IncludeExceptionDetailInFaults = true;

                    // Open the host: the REST service starts listening for HTTP requests.
                    host.Open();

                    Console.WriteLine("RoutingServiceREST self-hosted at:");
                    Console.WriteLine("  " + baseAddress);
                    Console.WriteLine();
                    Console.WriteLine("JCDECAUX_API_KEY loaded: " +
                        ConfigurationManager.AppSettings["JCDECAUX_API_KEY"]);
                    Console.WriteLine();
                    Console.WriteLine("Press ENTER to stop RoutingServiceREST...");
                    Console.ReadLine();

                    // Stop the service
                    host.Close();
                }
                catch (Exception ex)
                {
                    // If anything goes wrong, print the full exception to the console.
                    // This makes it easier to diagnose issues when running the .exe directly.
                    Console.WriteLine("Error starting RoutingServiceREST:");
                    Console.WriteLine(ex);
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                }
            }
        }
    }
}
