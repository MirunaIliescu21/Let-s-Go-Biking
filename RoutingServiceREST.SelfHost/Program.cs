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
        static void Main()
        {
            // Base address for the REST service.
            // It must match the address used previously in App.config and in the frontend.
            var baseAddress = new Uri("http://localhost:8733/Design_Time_Addresses/RoutingServiceREST/Service1/");

            // WebHttp binding configuration (copied from LargeWebBinding in the original App.config)
            var webBinding = new WebHttpBinding
            {
                Name = "LargeWebBinding",
                MaxReceivedMessageSize = 20000000,
                MaxBufferSize = 20000000,
                MaxBufferPoolSize = 20000000
            };

            // Reader quotas (must be assigned via a new instance)
            webBinding.ReaderQuotas = new XmlDictionaryReaderQuotas
            {
                MaxDepth = 64,
                MaxStringContentLength = 20000000,
                MaxArrayLength = 20000000,
                MaxBytesPerRead = 20000000,
                MaxNameTableCharCount = 20000000
            };

            using (var host = new WebServiceHost(typeof(RoutingService), baseAddress))
            {
                try
                {
                    // Main REST endpoint
                    var endpoint = host.AddServiceEndpoint(
                        typeof(IRoutingServiceREST),
                        webBinding,
                        ""
                    );

                    // Enable REST/JSON behavior
                    var webBehavior = new WebHttpBehavior
                    {
                        HelpEnabled = true,
                        DefaultOutgoingResponseFormat = WebMessageFormat.Json
                    };
                    endpoint.EndpointBehaviors.Add(webBehavior);

                    // Ensure there is exactly one ServiceDebugBehavior,
                    // because WebServiceHost may already contain one.
                    var debugBehavior = host.Description.Behaviors.Find<ServiceDebugBehavior>();
                    if (debugBehavior == null)
                    {
                        debugBehavior = new ServiceDebugBehavior();
                        host.Description.Behaviors.Add(debugBehavior);
                    }
                    debugBehavior.IncludeExceptionDetailInFaults = true;

                    // Open the host and start the service
                    host.Open();

                    Console.WriteLine("RoutingServiceREST self-hosted at:");
                    Console.WriteLine("  " + baseAddress);
                    Console.WriteLine();
                    Console.WriteLine("JCDECAUX_API_KEY loaded: " +
                        ConfigurationManager.AppSettings["JCDECAUX_API_KEY"]);
                    Console.WriteLine();
                    Console.WriteLine("Press ENTER to stop RoutingServiceREST...");
                    Console.ReadLine();

                    // Clean shutdown
                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error starting RoutingServiceREST:");
                    Console.WriteLine(ex);
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                }
            }
        }
    }
}
