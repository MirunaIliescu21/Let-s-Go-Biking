using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Xml; // for XmlDictionaryReaderQuotas
using ProxyCacheHarness.ProxyRef; // namespace-ul service reference-ului

class Program
{
    static void Main()
    {
        var address = new EndpointAddress("http://localhost:8734/Design_Time_Addresses/ProxyCacheService/Service1/");

        var binding = new BasicHttpBinding();
        binding.MaxReceivedMessageSize = int.MaxValue;
        binding.MaxBufferSize = int.MaxValue;
        binding.MaxBufferPoolSize = int.MaxValue;
        binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
        binding.TransferMode = TransferMode.Buffered;
        binding.MessageEncoding = WSMessageEncoding.Text;
        binding.SendTimeout = TimeSpan.FromMinutes(5);

        var client = new ProxyServiceClient(binding, address);

        // 1) Contracts (TTL 60s) – răspuns mic
        Console.WriteLine("Contracts test");
        Measure(() => client.GetJcdecauxContractsGeneric(60));

        // 2) Stations (TTL 5s) – răspuns mare
        Console.WriteLine("\nStations(lyon) test");
        Measure(() => client.GetJcdecauxStationsGeneric("lyon", 5));

        Console.WriteLine("\nAștept ~6s ca să expire TTL-ul pentru stations…");
        System.Threading.Thread.Sleep(6000);

        Console.WriteLine("\nStations(lyon) după expirare");
        Measure(() => client.GetJcdecauxStationsGeneric("lyon", 5));

        client.Close();
        Console.WriteLine("\nGata. Apasă Enter.");
        Console.ReadLine();
    }

    static void Measure(Func<string> fn)
    {
        var sw = Stopwatch.StartNew();
        var s = fn();
        sw.Stop();
        Console.WriteLine($"t = {sw.ElapsedMilliseconds} ms, len = {s?.Length ?? 0}");
    }
}
