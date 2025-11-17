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
            // Wire default services
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

                // Build response via planner
                var response = _planner.Plan(request, ctx);

                // Adding DEBUG info
                // Resolved coordinates + top 3 stations at origin / destination
                if (request.Debug)
                {
                    response.OriginResolvedLat = request.OriginLat;
                    response.OriginResolvedLon = request.OriginLon;
                    response.DestResolvedLat = request.DestLat;
                    response.DestResolvedLon = request.DestLon;

                    // Take only stations from the relevant contracts
                    var contracts = new System.Collections.Generic.HashSet<string>(
                        new[] { ctx.OriginContract, ctx.DestContract },
                        System.StringComparer.OrdinalIgnoreCase);

                    var stations = allStations
                        .Where(s => contracts.Contains(s.ContractName))
                        .ToList();

                    // Station with available bikes, sorted by distance from ORIGIN
                    var fromCandidates = stations
                        .Where(s => (s.TotalStands?.Availabilities?.Bikes ?? 0) > 0)
                        .Select(s => new
                        {
                            S = s,
                            DistM = GeoMath.HaversineMeters(
                                request.OriginLat, request.OriginLon,
                                s.Position.Lat, s.Position.Lon)
                        })
                        .OrderBy(x => x.DistM)
                        .ThenBy(x => x.S.Name)
                        .Take(3)
                        .ToList();

                    // Station with available stands, sorted by distance from DESTINATION
                    var toCandidates = stations
                        .Where(s => (s.TotalStands?.Availabilities?.Stands ?? 0) > 0)
                        .Select(s => new
                        {
                            S = s,
                            DistM = GeoMath.HaversineMeters(
                                request.DestLat, request.DestLon,
                                s.Position.Lat, s.Position.Lon)
                        })
                        .OrderBy(x => x.DistM)
                        .ThenBy(x => x.S.Name)
                        .Take(3)
                        .ToList();

                    response.BikeFromTop3 = fromCandidates
                        .Select(x => new ItineraryResponse.DebugStationChoice
                        {
                            Name = x.S.Name,
                            Lat = x.S.Position.Lat,
                            Lon = x.S.Position.Lon,
                            Bikes = x.S.TotalStands?.Availabilities?.Bikes ?? 0,
                            Stands = x.S.TotalStands?.Availabilities?.Stands ?? 0,
                            DistanceMeters = System.Math.Round(x.DistM, 1)
                        })
                        .ToList();

                    response.BikeToTop3 = toCandidates
                        .Select(x => new ItineraryResponse.DebugStationChoice
                        {
                            Name = x.S.Name,
                            Lat = x.S.Position.Lat,
                            Lon = x.S.Position.Lon,
                            Bikes = x.S.TotalStands?.Availabilities?.Bikes ?? 0,
                            Stands = x.S.TotalStands?.Availabilities?.Stands ?? 0,
                            DistanceMeters = System.Math.Round(x.DistM, 1)
                        })
                        .ToList();
                }

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
