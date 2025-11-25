using System;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Description;
using ProxyCacheService; // Namespace that contains ProxyService and IProxyService

namespace ProxyCacheService.SelfHost
{
    internal static class Program
    {
        /// <summary>
        /// Entry point for the self-hosted ProxyCacheService.
        /// This method creates a ServiceHost in code and exposes the SOAP service
        /// on the same URL and binding that were previously configured in App.config.
        /// </summary>
        static void Main()
        {
            // Base address of the SOAP service.
            // It matches exactly the one used in the original WCF config,
            // so existing clients (RoutingService, NotificationService) do not need any change.
            var baseAddress = new Uri("http://localhost:8734/Design_Time_Addresses/ProxyCacheService/Service1/");

            // Create the basicHttpBinding in code.
            // Settings are equivalent to the "BigBasic" binding from the original App.config.
            var binding = new BasicHttpBinding
            {
                Name = "BigBasic",
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                TransferMode = TransferMode.Buffered,
                MessageEncoding = WSMessageEncoding.Text,
                ReaderQuotas =
                {
                    MaxDepth = 64,
                    MaxStringContentLength = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxBytesPerRead = int.MaxValue,
                    MaxNameTableCharCount = int.MaxValue
                }
            };

            // Create the ServiceHost that will host ProxyService at the given base address.
            using (var host = new ServiceHost(typeof(ProxyService), baseAddress))
            {
                try
                {
                    // Main SOAP endpoint exposing IProxyService on the base address.
                    //host.AddServiceEndpoint(
                    //    typeof(IProxyService),
                    //    binding,
                    //    "" // empty relative address; exactly the baseAddress
                    //);
                    var endpoint = host.AddServiceEndpoint(
                        typeof(IProxyService),
                        binding,
                        "" // empty relative address; exactly the baseAddress
                    );

                    endpoint.Name = "BasicHttpBinding_IProxyService";


                    // Add metadata behavior so that the service exposes a WSDL (MEX endpoint).
                    var smb = new ServiceMetadataBehavior
                    {
                        HttpGetEnabled = true
                    };
                    host.Description.Behaviors.Add(smb);

                    // MEX endpoint used by tools such as WCF Test Client.
                    host.AddServiceEndpoint(
                        ServiceMetadataBehavior.MexContractName,
                        MetadataExchangeBindings.CreateMexHttpBinding(),
                        "mex"
                    );

                    // Open the ServiceHost to start listening for messages.
                    host.Open();

                    Console.WriteLine("ProxyCacheService self-hosted at:");
                    Console.WriteLine("  " + baseAddress);
                    Console.WriteLine();
                    Console.WriteLine("ORS_BEARER: " + ConfigurationManager.AppSettings["ORS_BEARER"]);
                    Console.WriteLine("JCDECAUX_API_KEY: " + ConfigurationManager.AppSettings["JCDECAUX_API_KEY"]);
                    Console.WriteLine();
                    Console.WriteLine("Press ENTER to stop ProxyCacheService...");
                    Console.ReadLine();

                    // Close the ServiceHost.
                    host.Close();
                }
                catch (Exception ex)
                {
                    // If anything goes wrong during startup or runtime, show the full exception
                    // so that debugging is easier when running the .exe directly.
                    Console.WriteLine("Error starting ProxyCacheService:");
                    Console.WriteLine(ex);
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                }
            }
        }
    }
}
