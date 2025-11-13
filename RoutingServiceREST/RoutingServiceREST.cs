using RoutingServiceREST;
using RoutingServiceREST.Planning;
using RoutingServiceREST.Services;
using RoutingServiceREST.Utils;
using System;
using System.Linq;
using System.ServiceModel.Web;


namespace RoutingServiceREST
{
    public class RoutingService : IRoutingServiceREST
    {
        private static void AllowCors()
        {
            var r = System.ServiceModel.Web.WebOperationContext.Current?.OutgoingResponse;
            if (r == null) return;
            r.Headers.Add("Access-Control-Allow-Origin", "*");
            r.Headers.Add("Access-Control-Allow-Headers", "content-type");
            r.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        }


        private readonly Services.IGeocodingService _geocoding;
        private readonly Services.IRoutingCoreService _routing;
        private readonly Services.IStationRepository _stations;
        private readonly Planning.IItineraryPlanner _planner;


        public RoutingService()
        {
            // Wire default services (simple DI)
            var proxyFactory = new Services.ProxyFactory();
            _geocoding = new Services.OrsGeocodingService(proxyFactory);
            _routing = new Services.OrsRoutingService(proxyFactory);
            _stations = new Services.JcdecauxStationRepository(proxyFactory);
            _planner = new Planning.ItineraryPlanner(_routing, new Planning.PlannerOptions());
        }


        public void Options() => AllowCors();
        public string Ping() { AllowCors(); return "pong"; }

        public ItineraryResponse GetItinerary(ItineraryRequest request)
        {
            AllowCors();
            try
            {
                // Resolve coordinates if needed
                if ((request.OriginLat, request.OriginLon) == (0, 0))
                {
                    if (!_geocoding.TryGeocode(request.Origin, out var o))
                        return Fail("Could not geocode Origin address.");
                    request.OriginLat = o.Lat; request.OriginLon = o.Lon;
                }
                if ((request.DestLat, request.DestLon) == (0, 0))
                {
                    if (!_geocoding.TryGeocode(request.Destination, out var d))
                        return Fail("Could not geocode Destination address.");
                    request.DestLat = d.Lat; request.DestLon = d.Lon;
                }


                var allStations = _stations.GetAllStations();
                if (allStations == null || allStations.Count == 0)
                    return Planning.WalkOnlyPlan.FromRouting(_routing, request, reason: "There is no useful JCDecaux network in the area.");


                var nearestToOrigin = Utils.StationPickers.Closest(allStations, request.OriginLat, request.OriginLon);
                var nearestToDest = Utils.StationPickers.Closest(allStations, request.DestLat, request.DestLon);
                if (nearestToOrigin == null || nearestToDest == null)
                    return Planning.WalkOnlyPlan.FromRouting(_routing, request, reason: "No nearby JCDecaux stations were found.");


                var ctx = new Planning.StationContext
                {
                    All = allStations,
                    OriginContract = nearestToOrigin.ContractName,
                    DestContract = nearestToDest.ContractName
                };


                var response = _planner.Plan(request, ctx);
                response.OriginResolvedLat = request.OriginLat;
                response.OriginResolvedLon = request.OriginLon;
                response.DestResolvedLat = request.DestLat;
                response.DestResolvedLon = request.DestLon;
                return response;
            }
            catch (System.Exception ex)
            {
                return Fail($"Error: {ex.Message}");
            }
        }


        public System.Collections.Generic.List<JcDecauxContract> GetContracts()
        {
            AllowCors();
            return ((Services.JcdecauxStationRepository)_stations).GetContracts();
        }


        public System.Collections.Generic.List<JcdecauxStation> GetStations(string contract)
        {
            AllowCors();
            return ((Services.JcdecauxStationRepository)_stations).GetStations(contract);
        }


        private static ItineraryResponse Fail(string msg) => new ItineraryResponse
        {
            Success = false,
            Message = msg,
            Instructions = System.Array.Empty<string>()
        };
    }
}


//using Newtonsoft.Json;
//using RoutingServiceREST.ProxyRef;  // Connected Service → ProxyCacheService
//using System;
//using System.Collections.Generic;
//using System.Globalization;
//using System.Linq;
//using System.ServiceModel.Web;
//using static RoutingServiceREST.ItineraryResponse;

//namespace RoutingServiceREST
//{
//    public class RoutingService : IRoutingServiceREST
//    {
//        private static void AllowCors()
//        {
//            var r = WebOperationContext.Current?.OutgoingResponse;
//            if (r == null) return;
//            r.Headers.Add("Access-Control-Allow-Origin", "*");
//            r.Headers.Add("Access-Control-Allow-Headers", "content-type");
//            r.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
//        }

//        // Plan 1: bike only in ORIGIN contract (walk at the end to Destination)
//        // This method builds an itinerary where the bike segment is entirely within
//        // the origin city's JCDecaux contract. It returns null if suitable stations
//        // (with bikes at pickup and stands at drop) are not available.
//        private ItineraryResponse PlanIntercityBikeAtOrigin(
//            ItineraryRequest req,
//            string originContract,
//            List<JcdecauxStation> allStations,
//            ProxyRef.ProxyServiceClient proxy)
//        {
//            var originStations = allStations.Where(s =>
//                string.Equals(s.ContractName, originContract, StringComparison.OrdinalIgnoreCase)).ToList();

//            var pickup = FindClosestStationWithBikes(originStations, req.OriginLat, req.OriginLon);
//            var drop = FindWithStandsNearestTo(originStations, req.DestLat, req.DestLon);
//            if (pickup == null || drop == null)
//                return null;

//            var walk1 = GetORSRoute("foot-walking",
//                req.OriginLon, req.OriginLat, pickup.Position.Lon, pickup.Position.Lat, proxy);
//            var bike = GetORSRoute("cycling-regular",
//                pickup.Position.Lon, pickup.Position.Lat, drop.Position.Lon, drop.Position.Lat, proxy);
//            var walk2 = GetORSRoute("foot-walking",
//                drop.Position.Lon, drop.Position.Lat, req.DestLon, req.DestLat, proxy);

//            var resp = new ItineraryResponse
//            {
//                Success = true,
//                Message = "MVP OK",
//                Instructions = new[]
//                {
//                    $"Start point: {req.Origin}",
//                    $"End point: {req.Destination}",
//                    $"Origin contract: {originContract}",
//                    $"Walk to station '{pickup.Name}'.",
//                    $"Ride to station '{drop.Name}'.",
//                    $"Walk to the destination."
//                },
//                UseBike = true,
//                OriginContract = originContract,
//                DestContract = null,
//                BikeFrom = pickup.Name,
//                BikeTo = drop.Name,

//                WalkOnlyDurationSec = GetORSRoute("foot-walking", req.OriginLon, req.OriginLat, req.DestLon, req.DestLat, proxy).DurationSec,
//                BikePlanDurationSec = walk1.DurationSec + bike.DurationSec + walk2.DurationSec,

//                Walk1Coords = walk1.Coords,
//                BikeCoords = bike.Coords,
//                Walk2Coords = walk2.Coords,

//                Walk1DistanceMeters = walk1.DistanceMeters,
//                BikeDistanceMeters = bike.DistanceMeters,
//                Walk2DistanceMeters = walk2.DistanceMeters,

//                BikePlanDistanceMeters = walk1.DistanceMeters + bike.DistanceMeters + walk2.DistanceMeters,

//                OriginResolvedLat = req.OriginLat,
//                OriginResolvedLon = req.OriginLon,
//                DestResolvedLat = req.DestLat,
//                DestResolvedLon = req.DestLon
//            };
//            return resp;
//        }

//        // Plan2: bike only in DEST contract (long walk into the city, then bike + short final walk)
//        // Similar to PlanIntercityBikeAtOrigin but uses the destination contract for biking.
//        private ItineraryResponse PlanIntercityBikeAtDestination(
//            ItineraryRequest req,
//            string destContract,
//            List<JcdecauxStation> allStations,
//            ProxyRef.ProxyServiceClient proxy)
//        {
//            var destStations = allStations.Where(s =>
//                string.Equals(s.ContractName, destContract, StringComparison.OrdinalIgnoreCase)).ToList();

//            // enter in city: take the first station in the destination contract that is "nearest to Origin"
//            var pickup = FindWithBikesNearestTo(destStations, req.OriginLat, req.OriginLon);
//            // closest station to Destination with stands available
//            var drop = FindWithStandsNearestTo(destStations, req.DestLat, req.DestLon);
//            if (pickup == null || drop == null)
//                return null;

//            var walk1 = GetORSRoute("foot-walking",
//                req.OriginLon, req.OriginLat, pickup.Position.Lon, pickup.Position.Lat, proxy);
//            var bike = GetORSRoute("cycling-regular",
//                pickup.Position.Lon, pickup.Position.Lat, drop.Position.Lon, drop.Position.Lat, proxy);
//            var walk2 = GetORSRoute("foot-walking",
//                drop.Position.Lon, drop.Position.Lat, req.DestLon, req.DestLat, proxy);

//            var resp = new ItineraryResponse
//            {
//                Success = true,
//                Message = "MVP OK",
//                Instructions = new[]
//                {
//                    $"Start point: {req.Origin}",
//                    $"End point: {req.Destination}",
//                    $"Destination contract: {destContract}",
//                    $"Walk to station '{pickup.Name}' (first station in the destination network).",
//                    $"Ride to station '{drop.Name}'.",
//                    $"Walk to the destination."
//                },
//                UseBike = true,
//                OriginContract = null,
//                DestContract = destContract,
//                BikeFrom = pickup.Name,
//                BikeTo = drop.Name,

//                WalkOnlyDurationSec = GetORSRoute("foot-walking", req.OriginLon, req.OriginLat, req.DestLon, req.DestLat, proxy).DurationSec,
//                BikePlanDurationSec = walk1.DurationSec + bike.DurationSec + walk2.DurationSec,

//                Walk1Coords = walk1.Coords,
//                BikeCoords = bike.Coords,
//                Walk2Coords = walk2.Coords,

//                Walk1DistanceMeters = walk1.DistanceMeters,
//                BikeDistanceMeters = bike.DistanceMeters,
//                Walk2DistanceMeters = walk2.DistanceMeters,

//                BikePlanDistanceMeters = walk1.DistanceMeters + bike.DistanceMeters + walk2.DistanceMeters,

//                OriginResolvedLat = req.OriginLat,
//                OriginResolvedLon = req.OriginLon,
//                DestResolvedLat = req.DestLat,
//                DestResolvedLon = req.DestLon
//            };
//            return resp;
//        }

//        public ItineraryResponse GetItinerary(ItineraryRequest request)
//        {
//            AllowCors();
//            if (request == null || string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
//                return Fail("Origin/Destination is missing.");

//            try
//            {
//                using (var proxy = new ProxyServiceClient())
//                {
//                    // GEOCODING (ORS) – staged, with clear messages
//                    try
//                    {
//                        if ((request.OriginLat == 0 && request.OriginLon == 0))
//                        {
//                            if (!TryGeocode(request.Origin, proxy, out var olat, out var olon))
//                                return Fail("Could not geocode Origin address.");
//                            request.OriginLat = olat;
//                            request.OriginLon = olon;
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        return Fail("ORS geocoding error (Origin): " + ex.Message);
//                    }

//                    try
//                    {
//                        if ((request.DestLat == 0 && request.DestLon == 0))
//                        {
//                            if (!TryGeocode(request.Destination, proxy, out var dlat, out var dlon))
//                                return Fail("Could not geocode Destination address.");
//                            request.DestLat = dlat;
//                            request.DestLon = dlon;
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        return Fail("ORS geocoding error (Destination): " + ex.Message);
//                    }

//                    // Adding info for debugging
//                    var resp = new ItineraryResponse();
//                    resp.OriginResolvedLat = request.OriginLat;
//                    resp.OriginResolvedLon = request.OriginLon;
//                    resp.DestResolvedLat = request.DestLat;
//                    resp.DestResolvedLon = request.DestLon;

//                    // JCDECAUX – list stations
//                    List<JcdecauxStation> allStations;
//                    try
//                    {
//                        allStations = LoadAllStationsViaProxy(proxy);
//                        if (allStations == null || allStations.Count == 0)
//                            //return DecideWalkOrFail(request, proxy,
//                            //    "JCDecaux API returned no stations for this area.");
//                            return BuildWalkOnlyPlan(request, proxy, "There is no useful JCDecaux network in the area.");

//                    }
//                    catch (Exception ex)
//                    {
//                        return BuildWalkOnlyPlan(request, proxy,
//                            "JCDecaux load error: " + ex.Message);
//                    }

//                    // Find the closest stations from any contract
//                    var nearestToOrigin = FindClosestStation(allStations, request.OriginLat, request.OriginLon);
//                    var nearestToDest = FindClosestStation(allStations, request.DestLat, request.DestLon);
//                    if (nearestToOrigin == null || nearestToDest == null)
//                        return BuildWalkOnlyPlan(request, proxy,
//                            "No nearby JCDecaux stations were found.");

//                    // Inter-city (different contracts): try best plan, but pick it only if it beats walk-only
//                    bool differentContracts = !string.Equals(nearestToOrigin.ContractName, nearestToDest.ContractName, StringComparison.OrdinalIgnoreCase);

//                    if (differentContracts)
//                    {
//                        string originContractCandidate = nearestToOrigin.ContractName;
//                        string destContractCandidate = nearestToDest.ContractName;

//                        // Baseline: walk-only between the 2 addresses
//                        var baselineWalk = GetORSRoute(
//                            "foot-walking",
//                            request.OriginLon, request.OriginLat,
//                            request.DestLon, request.DestLat,
//                            proxy);
//                        double baselineSec = baselineWalk.DurationSec;

//                        // Compute all bike plans (they can return null)
//                        var planOriginSide = PlanIntercityBikeAtOrigin(request, originContractCandidate, allStations, proxy);
//                        var planDestSide = PlanIntercityBikeAtDestination(request, destContractCandidate, allStations, proxy);
//                        var both = PlanIntercityBikeBothEnds(request, originContractCandidate, destContractCandidate, allStations, proxy);

//                        // Collect candidates that really beat walk-only (buffer 60s)
//                        var candidates = new List<ItineraryResponse>();
//                        void Consider(ItineraryResponse p)
//                        {
//                            if (p == null) return;
//                            if (p.BikePlanDurationSec + 60 < baselineSec) candidates.Add(p);
//                        }
//                        Consider(planOriginSide);
//                        Consider(planDestSide);

//                        // Both-ends is stricter: must beat BOTH walk-only AND the best single-side by ≥120s
//                        if (both != null)
//                        {
//                            double bestSingle = Math.Min(
//                                planOriginSide?.BikePlanDurationSec ?? double.MaxValue,
//                                planDestSide?.BikePlanDurationSec ?? double.MaxValue);

//                            if (both.BikePlanDurationSec + 120 < Math.Min(bestSingle, baselineSec))
//                                candidates.Add(both);
//                        }

//                        // If nothing is better than walking, return a full walk-only plan
//                        if (candidates.Count == 0)
//                            return BuildWalkOnlyPlan(request, proxy, "The origin and destination are in different JCDecaux networks (inter-city).");

//                        // Pick the fastest candidate
//                        var best = candidates.OrderBy(c => c.BikePlanDurationSec).First();

//                        // Make sure the context line is present
//                        var stepsLocal = new List<string>(best.Instructions ?? Array.Empty<string>());
//                        int insertAt = Math.Min(2, stepsLocal.Count);
//                        stepsLocal.Insert(insertAt, "The origin and destination are in different JCDecaux networks (inter-city).");
//                        best.Instructions = stepsLocal.ToArray();

//                        return best;
//                    }



//                    // It's possible to have a relevant network
//                    string originContract = nearestToOrigin.ContractName;
//                    string destContract = nearestToDest.ContractName;

//                    // Filter stations by relevant contracts
//                    var contracts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
//                    { originContract, destContract };
//                    var stations = allStations.Where(s => contracts.Contains(s.ContractName)).ToList();

//                    // Stations near Origin that have stands available, sorted in ascending order
//                    var fromCandidates = stations
//                        .Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
//                        .Select(s => new
//                        {
//                            S = s,
//                            DistM = HaversineMeters(request.OriginLat, request.OriginLon, s.Position.Lat, s.Position.Lon)
//                        })
//                        .OrderBy(x => x.DistM)
//                        .Take(3)
//                        .ToList();      // take the first 3 closest ones

//                    // Stations near Destianation that have stands available, sorted in ascending order
//                    var toCandidates = stations
//                        .Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
//                        .Select(s => new
//                        {
//                            S = s,
//                            DistM = HaversineMeters(request.DestLat, request.DestLon, s.Position.Lat, s.Position.Lon)
//                        })
//                        .OrderBy(x => x.DistM)
//                        .Take(3)
//                        .ToList();      // take the first 3 closest ones

//                    // The winning stations
//                    var bikeFrom = fromCandidates.OrderBy(x => x.DistM).ThenBy(x => x.S.Name).FirstOrDefault()?.S;
//                    var bikeTo = toCandidates.OrderBy(x => x.DistM).ThenBy(x => x.S.Name).FirstOrDefault()?.S;

//                    if (bikeFrom == null || bikeTo == null)
//                        return BuildWalkOnlyPlan(request, proxy, "No bikes near us.");

//                    // If the user requests debug, I populate the top-3 station in the response
//                    if (request.Debug)
//                    {
//                        resp.BikeFromTop3 = fromCandidates.Select(x => new DebugStationChoice
//                        {
//                            Name = x.S.Name,
//                            Lat = x.S.Position.Lat,
//                            Lon = x.S.Position.Lon,
//                            Bikes = x.S.TotalStands?.Availabilities?.Bikes ?? 0,
//                            Stands = x.S.TotalStands?.Availabilities?.Stands ?? 0,
//                            DistanceMeters = Math.Round(x.DistM, 1)
//                        }).ToList();

//                        resp.BikeToTop3 = toCandidates.Select(x => new DebugStationChoice
//                        {
//                            Name = x.S.Name,
//                            Lat = x.S.Position.Lat,
//                            Lon = x.S.Position.Lon,
//                            Bikes = x.S.TotalStands?.Availabilities?.Bikes ?? 0,
//                            Stands = x.S.TotalStands?.Availabilities?.Stands ?? 0,
//                            DistanceMeters = Math.Round(x.DistM, 1)
//                        }).ToList();
//                    }
//                    // Stop debug

//                    // Compare walk-only vs bike plan (ORS durations)
//                    var walkOnly = GetORSRoute("foot-walking",
//                        request.OriginLon, request.OriginLat, request.DestLon, request.DestLat, proxy);

//                    var walk1 = GetORSRoute("foot-walking",
//                        request.OriginLon, request.OriginLat, bikeFrom.Position.Lon, bikeFrom.Position.Lat, proxy);
//                    var bike = GetORSRoute("cycling-regular",
//                        bikeFrom.Position.Lon, bikeFrom.Position.Lat, bikeTo.Position.Lon, bikeTo.Position.Lat, proxy);
//                    var walk2 = GetORSRoute("foot-walking",
//                        bikeTo.Position.Lon, bikeTo.Position.Lat, request.DestLon, request.DestLat, proxy);

//                    // Calculate durations by foot and by bike and compare them
//                    double walkOnlySec = walkOnly.DurationSec;
//                    double bikePlanSec = walk1.DurationSec + bike.DurationSec + walk2.DurationSec;

//                    bool useBike = bikePlanSec + 60 < walkOnlySec; // small buffer (1 min) for lock/unlock etc.

//                    double walkOnlyMeters = walkOnly.DistanceMeters;
//                    double bikePlanMeters = walk1.DistanceMeters + bike.DistanceMeters + walk2.DistanceMeters;

//                    // Instructions
//                    var steps = new List<string>
//                    {
//                        $"Start point: {request.Origin}",
//                        $"End point: {request.Destination}",
//                        $"Origin contract: {originContract}",
//                        $"Destination contract: {destContract}"
//                    };

//                    if (useBike)
//                    {
//                        steps.Add($"Walk to station '{bikeFrom.Name}'.");
//                        steps.Add($"Pick up a bike and ride to station '{bikeTo.Name}'.");
//                        steps.Add($"Drop the bike and walk to the destination.");
//                    }
//                    else
//                    {
//                        steps.Add("Walk to the destination (bike does not provide significant benefit).");
//                    }

//                    resp.Success = true;
//                    resp.Message = "MVP OK";
//                    resp.Instructions = steps.ToArray();
//                    resp.UseBike = useBike;
//                    resp.OriginContract = originContract;
//                    resp.DestContract = destContract;
//                    resp.BikeFrom = bikeFrom.Name;
//                    resp.BikeTo = bikeTo.Name;
//                    resp.WalkOnlyDurationSec = Math.Round(walkOnlySec, 1);
//                    resp.BikePlanDurationSec = Math.Round(bikePlanSec, 1);
//                    resp.Walk1Coords = walk1.Coords;
//                    resp.BikeCoords = bike.Coords;
//                    resp.Walk2Coords = walk2.Coords;
//                    resp.WalkOnlyDistanceMeters = Math.Round(walkOnlyMeters, 1);
//                    resp.BikePlanDistanceMeters = Math.Round(bikePlanMeters, 1);
//                    resp.Walk1DistanceMeters = Math.Round(walk1.DistanceMeters, 1);
//                    resp.BikeDistanceMeters = Math.Round(bike.DistanceMeters, 1);
//                    resp.Walk2DistanceMeters = Math.Round(walk2.DistanceMeters, 1);
//                    return resp;

//                }
//            }
//            catch (Exception ex)
//            {
//                return Fail($"Error: {ex.Message}");
//            }
//        }

//        public void Options() => AllowCors();

//        public string Ping()
//        {
//            AllowCors();
//            return "pong";
//        }

//        public List<JcDecauxContract> GetContracts()
//        {
//            AllowCors();
//            using (var proxy = new ProxyRef.ProxyServiceClient())
//            {
//                string url = "https://api.jcdecaux.com/vls/v3/contracts";
//                string json = proxy.GetWithTtl(url, 3600, false, false);
//                var trimmed = json?.TrimStart() ?? "";

//                if (string.IsNullOrEmpty(trimmed))
//                    throw new Exception("JCDecaux empty response.");

//                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                    throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));

//                try
//                {
//                    // Deserialize directly into typed objects
//                    var contracts = JsonConvert.DeserializeObject<List<JcDecauxContract>>(json)
//                                    ?? new List<JcDecauxContract>();

//                    // Normalize null lists for safer usage later
//                    foreach (var c in contracts)
//                    {
//                        if (c.Cities == null)
//                        {
//                            c.Cities = new List<string>();
//                        }
//                    }

//                    return contracts;
//                }
//                catch (Exception ex)
//                {
//                    throw new Exception("Parse error (contracts): " + ex.Message);
//                }
//            }
//        }

//        public List<JcdecauxStation> GetStations(string contract)
//        {
//            AllowCors();
//            using (var proxy = new ProxyRef.ProxyServiceClient())
//            {
//                string baseUrl = "https://api.jcdecaux.com/vls/v3/stations";
//                string url = string.IsNullOrWhiteSpace(contract)
//                    ? $"{baseUrl}"
//                    : $"{baseUrl}?contract={Uri.EscapeDataString(contract)}";


//                string json = proxy.GetWithTtl(url, 60, false, false);
//                var trimmed = json?.TrimStart() ?? "";

//                if (string.IsNullOrEmpty(trimmed))
//                    throw new Exception("JCDecaux empty response.");

//                if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                    throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));

//                try
//                {
//                    return JsonConvert.DeserializeObject<List<JcdecauxStation>>(json);
//                }
//                catch (Exception ex)
//                {
//                    throw new Exception("Parse error (stations): " + ex.Message);
//                }
//            }
//        }


//        private static ItineraryResponse Fail(string msg) => new ItineraryResponse
//        {
//            Success = false,
//            Message = msg,
//            Instructions = Array.Empty<string>()
//        };

//        // Always build a full walk-only itinerary when bike is not feasible/available.
//        // This returns a COMPLETE ItineraryResponse with route coords so the UI
//        // can render the walking path on a map.
//        private ItineraryResponse BuildWalkOnlyPlan(ItineraryRequest req, ProxyRef.ProxyServiceClient proxy, string reason)
//        {
//            // 1) Get walking route from ORS so we have distance, duration, and a polyline for the map
//            var walk = GetORSRoute(
//                "foot-walking",
//                req.OriginLon, req.OriginLat,
//                req.DestLon, req.DestLat,
//                proxy);

//            var steps = new List<string>
//            {
//                $"Start point: {req.Origin}",
//                $"End point: {req.Destination}",
//                string.IsNullOrWhiteSpace(reason)
//                    ? "There is no useful JCDecaux network in the area."
//                    : reason,
//                "Walk to the destination (bike is not feasible in this scenario)."
//            };

//            // 2) Return a COMPLETE response so debug/UI can render the path
//            return new ItineraryResponse
//            {
//                Success = true,
//                Message = "MVP complet OK",
//                Instructions = steps.ToArray(),

//                UseBike = false,
//                OriginContract = null,
//                DestContract = null,
//                BikeFrom = null,
//                BikeTo = null,

//                WalkOnlyDurationSec = Math.Round(walk.DurationSec, 1),
//                BikePlanDurationSec = 0,

//                WalkOnlyDistanceMeters = Math.Round(walk.DistanceMeters, 1),
//                BikePlanDistanceMeters = 0,

//                Walk1Coords = walk.Coords,   // draw this on the map
//                BikeCoords = null,
//                Walk2Coords = null,

//                Walk1DistanceMeters = Math.Round(walk.DistanceMeters, 1),
//                BikeDistanceMeters = 0,
//                Walk2DistanceMeters = 0,

//                OriginResolvedLat = req.OriginLat,
//                OriginResolvedLon = req.OriginLon,
//                DestResolvedLat = req.DestLat,
//                DestResolvedLon = req.DestLon,

//                BikeFromTop3 = null,
//                BikeToTop3 = null
//            };
//        }

//        private static List<JcdecauxStation> LoadAllStationsViaProxy(ProxyServiceClient proxy)
//        {
//            string url = "https://api.jcdecaux.com/vls/v3/stations";
//            string json = proxy.GetWithTtl(url, 60, false, false);
//            var trimmed = json?.TrimStart() ?? "";

//            if (string.IsNullOrEmpty(trimmed))
//                throw new Exception("JCDecaux empty response.");

//            if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                throw new Exception("JCDecaux non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));

//            return JsonConvert.DeserializeObject<List<JcdecauxStation>>(json);
//        }

//        private static JcdecauxStation FindClosestStation(List<JcdecauxStation> stations, double lat, double lon)
//        {
//            return stations
//                .OrderBy(s => HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon))
//                .FirstOrDefault();
//        }

//        private static JcdecauxStation FindClosestStationWithBikes(List<JcdecauxStation> stations, double lat, double lon)
//        {
//            return stations
//                .Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
//                .OrderBy(s => HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon))
//                .FirstOrDefault();
//        }

//        private static JcdecauxStation FindClosestStationWithStands(List<JcdecauxStation> stations, double lat, double lon)
//        {
//            return stations
//                .Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
//                .OrderBy(s => HaversineKm(lat, lon, s.Position.Lat, s.Position.Lon))
//                .FirstOrDefault();
//        }

//        private static JcdecauxStation FindWithBikesNearestTo(
//            IEnumerable<JcdecauxStation> stations, double targetLat, double targetLon)
//        {
//            return stations
//                .Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
//                .OrderBy(s => HaversineKm(targetLat, targetLon, s.Position.Lat, s.Position.Lon))
//                .FirstOrDefault();
//        }

//        private static JcdecauxStation FindWithStandsNearestTo(
//            IEnumerable<JcdecauxStation> stations, double targetLat, double targetLon)
//        {
//            return stations
//                .Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
//                .OrderBy(s => HaversineKm(targetLat, targetLon, s.Position.Lat, s.Position.Lon))
//                .FirstOrDefault();
//        }


//        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
//        {
//            const double R = 6371.0;
//            double dLat = (lat2 - lat1) * Math.PI / 180.0;
//            double dLon = (lon2 - lon1) * Math.PI / 180.0;
//            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
//                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
//                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
//            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
//            return R * c;
//        }

//        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
//        {
//            return HaversineKm(lat1, lon1, lat2, lon2) * 1000.0;
//        }

//        // Function used to prefer "city" results in geocoding.
//        // The name has only one word, without "," or numbers etc. - it looks ambiguase
//        private static bool IsLikelyCityQuery(string s)
//        {
//            if (string.IsNullOrWhiteSpace(s)) return false;
//            if (s.IndexOf(',') >= 0) return false;           // "Lyon, France" is clear, we don't do anything
//            if (s.Any(char.IsDigit)) return false;           // "221B Baker Street" is clear, we don't do anything
//            var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
//            return parts.Length <= 3;                         // It's only a short name (e.g. "Lyon", "New York")
//        }

//        // Geocodare ORS (forward) — city-first
//        // This method prefers locality-like results when the query looks like a city name.
//        private static bool TryGeocode(string text, ProxyServiceClient proxy, out double lat, out double lon)
//        {
//            lat = 0; lon = 0;
//            if (string.IsNullOrWhiteSpace(text)) return false;

//            string baseUrl = "https://api.openrouteservice.org/geocode/search";
//            bool cityMode = IsLikelyCityQuery(text);

//            // City-first mode: layers=locality,localadmin,county,region,macroregion
//            string url = $"{baseUrl}?text={Uri.EscapeDataString(text)}&size=1" +
//                         (cityMode ? "&layers=locality,localadmin,county,region,macroregion" : "");

//            string json = proxy.GetWithTtl(url, 86400, false, false);
//            var trimmed = json?.TrimStart() ?? "";
//            if (string.IsNullOrEmpty(trimmed))
//                throw new Exception("ORS geocode empty response.");
//            if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                throw new Exception("ORS geocode non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));

//            dynamic doc = JsonConvert.DeserializeObject(json);
//            if (doc?.features == null || doc.features.Count == 0)
//            {
//                // fallback: generic search 
//                if (cityMode)
//                {
//                    url = $"{baseUrl}?text={Uri.EscapeDataString(text)}&size=1";
//                    json = proxy.GetWithTtl(url, 86400, false, false);
//                    trimmed = json?.TrimStart() ?? "";
//                    if (string.IsNullOrEmpty(trimmed)) return false;
//                    if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                        throw new Exception("ORS geocode non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));
//                    doc = JsonConvert.DeserializeObject(json);
//                }
//                if (doc?.features == null || doc.features.Count == 0) return false;
//            }

//            double dlon = (double)doc.features[0].geometry.coordinates[0];
//            double dlat = (double)doc.features[0].geometry.coordinates[1];
//            lat = dlat; lon = dlon;
//            return true;
//        }

//        // ORS Directions (duration + optional steps)
//        // This method parses ORS JSON and returns a compact OrsRoute with
//        // duration, distance, a list of human-readable steps and coordinates
//        private class OrsRoute
//        {
//            public double DurationSec { get; set; }
//            public double DistanceMeters { get; set; }
//            public List<string> Steps { get; set; } = new List<string>();
//            public List<double[]> Coords { get; set; } = new List<double[]>(); // [lat, lon]
//        }

//        private static OrsRoute GetORSRoute(
//            string profile,
//            double startLon, double startLat, double endLon, double endLat,
//            ProxyServiceClient proxy)
//        {
//            string url =
//                $"https://api.openrouteservice.org/v2/directions/{profile}" +
//                $"?start={startLon.ToString(CultureInfo.InvariantCulture)},{startLat.ToString(CultureInfo.InvariantCulture)}" +
//                $"&end={endLon.ToString(CultureInfo.InvariantCulture)},{endLat.ToString(CultureInfo.InvariantCulture)}";

//            string json = proxy.GetWithTtl(url, 120, false, false);
//            var trimmed = json?.TrimStart() ?? "";

//            if (string.IsNullOrEmpty(trimmed))
//                throw new Exception("ORS directions empty response.");

//            if (trimmed.StartsWith("<") || trimmed.StartsWith("("))
//                throw new Exception("ORS directions non-JSON: " + trimmed.Substring(0, Math.Min(200, trimmed.Length)));

//            dynamic doc = JsonConvert.DeserializeObject(json);
//            var seg = doc.features[0].properties.segments[0];

//            double dur = (double)seg.duration;   // seconds
//            double dist = (double)seg.distance;  // meters


//            var steps = new List<string>();
//            foreach (var s in doc.features[0].properties.segments[0].steps)
//            {
//                string instr = (string)s.instruction;
//                if (!string.IsNullOrWhiteSpace(instr))
//                {
//                    steps.Add(instr);
//                }
//            }

//            var coords = new List<double[]>();
//            foreach (var p in doc.features[0].geometry.coordinates)
//            {
//                double lon = (double)p[0];
//                double lat = (double)p[1];
//                coords.Add(new[] { lat, lon }); // [lat,lon] for Leaflet
//            }


//            return new OrsRoute { DurationSec = dur, DistanceMeters = dist, Steps = steps, Coords = coords };
//        }

//        private static ItineraryResponse.RouteSegment Seg(string mode, string contract,
//                                                          string fromName, string toName, OrsRoute r)
//        {
//            return new ItineraryResponse.RouteSegment
//            {
//                Mode = mode,
//                Contract = contract,
//                FromName = fromName,
//                ToName = toName,
//                Coords = r?.Coords ?? new List<double[]>(),
//                DistanceMeters = r?.DistanceMeters ?? 0,
//                DurationSec = r?.DurationSec ?? 0
//            };
//        }

//        // Bike both ends (long walk between cities)
//        // This plan is used when both origin and destination networks can provide
//        // a bike segment on each city and a long inter-city walk/transfer exists.
//        private ItineraryResponse PlanIntercityBikeBothEnds(
//            ItineraryRequest req,
//            string originContract,
//            string destContract,
//            List<JcdecauxStation> allStations,
//            ProxyRef.ProxyServiceClient proxy)
//        {
//            // 1) stații din contractul de origine
//            var originStations = allStations.Where(s =>
//                string.Equals(s.ContractName, originContract, StringComparison.OrdinalIgnoreCase)).ToList();

//            // 2) stații din contractul de destinație
//            var destStations = allStations.Where(s =>
//                string.Equals(s.ContractName, destContract, StringComparison.OrdinalIgnoreCase)).ToList();

//            // ——— ORIGIN SIDE ———
//            var oPickup = FindClosestStationWithBikes(originStations, req.OriginLat, req.OriginLon);
//            // “ieșire” din orașul de origine: cea mai aproape de coordonatele destinației
//            var oDrop = FindWithStandsNearestTo(originStations, req.DestLat, req.DestLon);

//            // ——— DEST SIDE ———
//            // “intrare” în orașul de destinație: cea mai aproape de coordonatele originii
//            var dPickup = FindWithBikesNearestTo(destStations, req.OriginLat, req.OriginLon);
//            var dDrop = FindWithStandsNearestTo(destStations, req.DestLat, req.DestLon);

//            // dacă nu avem măcar câte o pereche validă la ambele capete → renunțăm la acest plan
//            if (oPickup == null || oDrop == null || dPickup == null || dDrop == null)
//                return null;

//            // ——— segmente rulate la ORS ———
//            var walk0 = GetORSRoute("foot-walking",
//                req.OriginLon, req.OriginLat, oPickup.Position.Lon, oPickup.Position.Lat, proxy);

//            var bike0 = GetORSRoute("cycling-regular",
//                oPickup.Position.Lon, oPickup.Position.Lat, oDrop.Position.Lon, oDrop.Position.Lat, proxy);

//            // mers “inter-city” între capete (poate fi lung; profu’ a cerut să existe rută oricum)
//            var walkMid = GetORSRoute("foot-walking",
//                oDrop.Position.Lon, oDrop.Position.Lat, dPickup.Position.Lon, dPickup.Position.Lat, proxy);

//            var bike1 = GetORSRoute("cycling-regular",
//                dPickup.Position.Lon, dPickup.Position.Lat, dDrop.Position.Lon, dDrop.Position.Lat, proxy);

//            var walk2 = GetORSRoute("foot-walking",
//                dDrop.Position.Lon, dDrop.Position.Lat, req.DestLon, req.DestLat, proxy);

//            // totaluri
//            double bikePlanSec = walk0.DurationSec + bike0.DurationSec + walkMid.DurationSec + bike1.DurationSec + walk2.DurationSec;
//            double bikePlanMeters = walk0.DistanceMeters + bike0.DistanceMeters + walkMid.DistanceMeters + bike1.DistanceMeters + walk2.DistanceMeters;

//            var walkOnly = GetORSRoute("foot-walking",
//                req.OriginLon, req.OriginLat, req.DestLon, req.DestLat, proxy);

//            bool useBike = bikePlanSec + 60 < walkOnly.DurationSec;

//            // Instrucțiuni
//            var steps = new List<string>
//                    {
//                        $"Start point: {req.Origin}",
//                        $"End point: {req.Destination}",
//                        "The origin and destination are in different JCDecaux networks (inter-city).",
//                        $"Origin contract: {originContract}",
//                        $"Destination contract: {destContract}"
//                    };
//            steps.Add($"Walk to station '{oPickup.Name}'.");
//            steps.Add($"Ride to station '{oDrop.Name}'.");
//            steps.Add($"Walk (inter-city) to station '{dPickup.Name}'.");
//            steps.Add($"Ride to station '{dDrop.Name}'.");
//            steps.Add("Walk to the destination.");

//            var resp = new ItineraryResponse
//            {
//                Success = true,
//                Message = "MVP OK — both-ends bike",
//                Instructions = steps.ToArray(),
//                UseBike = useBike,

//                // păstrăm câmpurile clasice cu niște valori reprezentative
//                OriginContract = originContract,
//                DestContract = destContract,
//                BikeFrom = oPickup.Name,
//                BikeTo = dDrop.Name,

//                WalkOnlyDurationSec = Math.Round(walkOnly.DurationSec, 1),
//                BikePlanDurationSec = Math.Round(bikePlanSec, 1),

//                // pentru compat (UI vechi), punem doar primul bike în BikeCoords;
//                // restul traseului e în Segments (nou)
//                Walk1Coords = walk0.Coords,
//                BikeCoords = bike0.Coords,
//                Walk2Coords = walk2.Coords,

//                WalkOnlyDistanceMeters = Math.Round(walkOnly.DistanceMeters, 1),
//                BikePlanDistanceMeters = Math.Round(bikePlanMeters, 1),
//                Walk1DistanceMeters = Math.Round(walk0.DistanceMeters, 1),
//                BikeDistanceMeters = Math.Round(bike0.DistanceMeters, 1),
//                Walk2DistanceMeters = Math.Round(walk2.DistanceMeters, 1),

//                OriginResolvedLat = req.OriginLat,
//                OriginResolvedLon = req.OriginLon,
//                DestResolvedLat = req.DestLat,
//                DestResolvedLon = req.DestLon,

//                Segments = new List<ItineraryResponse.RouteSegment>
//                {
//                    Seg("walk", null, "Origin", oPickup.Name, walk0),
//                    Seg("bike", originContract, oPickup.Name, oDrop.Name, bike0),
//                    Seg("walk", null, oDrop.Name, dPickup.Name, walkMid),
//                    Seg("bike", destContract, dPickup.Name, dDrop.Name, bike1),
//                    Seg("walk", null, dDrop.Name, "Destination", walk2)
//                }
//            };

//            return resp;
//        }
//    }
//}

