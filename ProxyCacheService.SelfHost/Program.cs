using System;
using System.Configuration;
using System.ServiceModel;
using System.ServiceModel.Description;
using ProxyCacheService; // namespace-ul în care ai ProxyService

namespace ProxyCacheService.SelfHost
{
    internal static class Program
    {
        static void Main()
        {
            // Adresa de bază — FOARTE IMPORTANT:
            // folosim EXACT aceeași ca în App.config-ul vechi,
            // ca să nu fie nevoie să schimbi RoutingService și NotificationService.
            var baseAddress = new Uri("http://localhost:8734/Design_Time_Addresses/ProxyCacheService/Service1/");

            // Cream binding-ul în cod, copiat după BigBasic din App.config
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

            using (var host = new ServiceHost(typeof(ProxyService), baseAddress))
            {
                try
                {
                    // Endpoint principal SOAP
                    host.AddServiceEndpoint(
                        typeof(IProxyService),
                        binding,
                        "" // address relativ = "", deci e fix baseAddress-ul de mai sus
                    );

                    // MEX — pentru WCF Test Client, debugging
                    var smb = new ServiceMetadataBehavior
                    {
                        HttpGetEnabled = true
                    };
                    host.Description.Behaviors.Add(smb);
                    host.AddServiceEndpoint(
                        ServiceMetadataBehavior.MexContractName,
                        MetadataExchangeBindings.CreateMexHttpBinding(),
                        "mex"
                    );

                    host.Open();

                    Console.WriteLine("ProxyCacheService self-hosted at:");
                    Console.WriteLine("  " + baseAddress);
                    Console.WriteLine();
                    Console.WriteLine("ORS_BEARER: " + ConfigurationManager.AppSettings["ORS_BEARER"]);
                    Console.WriteLine("JCDECAUX_API_KEY: " + ConfigurationManager.AppSettings["JCDECAUX_API_KEY"]);
                    Console.WriteLine();
                    Console.WriteLine("Press ENTER to stop ProxyCacheService...");
                    Console.ReadLine();

                    host.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error starting ProxyCacheService:");
                    Console.WriteLine(ex);
                    Console.WriteLine("Press ENTER to exit.");
                    Console.ReadLine();
                }
            }
        }
    }
}
