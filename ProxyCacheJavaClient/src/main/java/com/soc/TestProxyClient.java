package com.soc;

import com.soap.proxycache.client.generated.IProxyService;
import com.soap.proxycache.client.generated.ProxyService;

public class TestProxyClient {

    public static void main(String[] args) {
        System.out.println("Testing Java heavy SOAP client for ProxyCacheService...");

        // The ProxyService class is generate by WSDL
        ProxyService service = new ProxyService();
        // Obtain the proxy's port for this service
        IProxyService port = service.getBasicHttpBindingIProxyService();

        // Get JCDecaux contracts from generic cache
        String contractsJson = port.getJcdecauxContractsGeneric(3600);
        System.out.println("=== Contracts JSON (generic cache) ===");
        System.out.println(contractsJson);

        // Get JCDecaux stations from generic cache for a specific contract (ex: lyon)
        String lyonStations = port.getJcdecauxStationsGeneric("lyon", 30);
        System.out.println("\n=== Stations for lyon (generic cache) ===");
        System.out.println(lyonStations);

        // Status after generic cache (does not affect hits/misses; it is just to illustrate the genericity)
        String statsBefore = port.status();
        System.out.println("\n=== Stats after generic cache calls ===");
        System.out.println(statsBefore);

        // Demonstrate URL-based caching + HIT/MISS
        String testUrl = "https://api.jcdecaux.com/vls/v3/contracts";

        System.out.println("\n=== URL cache demonstration with GetWithMeta ===");

        // First call -> MISS and, if the response is 200, MISS->CACHED
        String meta1 = port.getWithMeta(testUrl, 30.0);
        System.out.println("First call (expected MISS / MISS->CACHED):");
        System.out.println(meta1);
        System.out.println("Stats after first call:");
        System.out.println(port.status());

        // Second call with the same URL -> HIT
        String meta2 = port.getWithMeta(testUrl, 30.0);
        System.out.println("\nSecond call (expected HIT):");
        System.out.println(meta2);
        System.out.println("Stats after second call:");
        System.out.println(port.status());

        // Evict the URL from the cache and call it again (to force a MISS again)
        port.evict(testUrl);
        System.out.println("\nEvicted URL from cache.");

        String meta3 = port.getWithMeta(testUrl, 30.0);
        System.out.println("Third call after eviction (expected MISS again):");
        System.out.println(meta3);
        System.out.println("Stats after third call:");
        System.out.println(port.status());
    }
}
