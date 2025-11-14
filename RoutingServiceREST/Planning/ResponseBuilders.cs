using System.Collections.Generic;
using RoutingServiceREST.Services;


namespace RoutingServiceREST.Planning
{
    public static class ResponseBuilders
    {
        public static ItineraryResponse OriginOnly(ItineraryRequest req, string originContract,
                                                JcdecauxStation pickup, JcdecauxStation drop,
                                                OrsRoute walk1, OrsRoute bike, OrsRoute walk2,
                                                IRoutingCoreService routing)
        {
            var walkOnly = routing.GetRoute("foot-walking", new LatLon(req.OriginLat, req.OriginLon), new LatLon(req.DestLat, req.DestLon));
            return new ItineraryResponse
            {
                Success = true,
                Message = "MVP OK",
                Instructions = new[]
                {
                    $"Start point: {req.Origin}",
                    $"End point: {req.Destination}",
                    $"Origin contract: {originContract}",
                    $"Walk to station '{pickup.Name}'.",
                    $"Ride to station '{drop.Name}'.",
                    "Walk to the destination."
                },
                UseBike = true,
                OriginContract = originContract,
                BikeFrom = pickup.Name,
                BikeTo = drop.Name,
                WalkOnlyDurationSec = walkOnly.DurationSec,
                BikePlanDurationSec = walk1.DurationSec + bike.DurationSec + walk2.DurationSec,
                Walk1Coords = walk1.Coords,
                BikeCoords = bike.Coords,
                Walk2Coords = walk2.Coords,
                Walk1DistanceMeters = walk1.DistanceMeters,
                BikeDistanceMeters = bike.DistanceMeters,
                Walk2DistanceMeters = walk2.DistanceMeters,
                BikePlanDistanceMeters = walk1.DistanceMeters + bike.DistanceMeters + walk2.DistanceMeters,

                Segments = new List<ItineraryResponse.RouteSegment>
                {
                    Seg("walk", null, "Origin", pickup.Name, walk1),
                    Seg("bike", originContract, pickup.Name, drop.Name, bike),
                    Seg("walk", null, drop.Name, "Destination", walk2)
                }
            };
        }

        public static ItineraryResponse DestinationOnly(ItineraryRequest req, string destContract,
                                                        JcdecauxStation pickup, JcdecauxStation drop,
                                                        OrsRoute walk1, OrsRoute bike, OrsRoute walk2,
                                                        IRoutingCoreService routing)
        {
            var walkOnly = routing.GetRoute("foot-walking", new LatLon(req.OriginLat, req.OriginLon), new LatLon(req.DestLat, req.DestLon));
            return new ItineraryResponse
            {
                Success = true,
                Message = "MVP OK",
                Instructions = new[]
                {
                    $"Start point: {req.Origin}",
                    $"End point: {req.Destination}",
                    $"Destination contract: {destContract}",
                    $"Walk to station '{pickup.Name}' (first station in the destination network).",
                    $"Ride to station '{drop.Name}'.",
                    "Walk to the destination."
                },
                UseBike = true,
                DestContract = destContract,
                BikeFrom = pickup.Name,
                BikeTo = drop.Name,
                WalkOnlyDurationSec = walkOnly.DurationSec,
                BikePlanDurationSec = walk1.DurationSec + bike.DurationSec + walk2.DurationSec,
                Walk1Coords = walk1.Coords,
                BikeCoords = bike.Coords,
                Walk2Coords = walk2.Coords,
                Walk1DistanceMeters = walk1.DistanceMeters,
                BikeDistanceMeters = bike.DistanceMeters,
                Walk2DistanceMeters = walk2.DistanceMeters,
                BikePlanDistanceMeters = walk1.DistanceMeters + bike.DistanceMeters + walk2.DistanceMeters,

                Segments = new List<ItineraryResponse.RouteSegment>
                {
                    Seg("walk", null, "Origin", pickup.Name, walk1),
                    Seg("bike", destContract, pickup.Name, drop.Name, bike),
                    Seg("walk", null, drop.Name, "Destination", walk2)
                }
            };
        }

        public static ItineraryResponse BothEnds(ItineraryRequest req, string originContract, string destContract,
                                                JcdecauxStation oPickup, JcdecauxStation oDrop, JcdecauxStation dPickup, JcdecauxStation dDrop,
                                                OrsRoute walk0, OrsRoute bike0, OrsRoute walkMid, OrsRoute bike1, OrsRoute walk2, IRoutingCoreService routing)
        {
            var walkOnly = routing.GetRoute("foot-walking", new LatLon(req.OriginLat, req.OriginLon), new LatLon(req.DestLat, req.DestLon));
            double bikePlanSec = walk0.DurationSec + bike0.DurationSec + walkMid.DurationSec + bike1.DurationSec + walk2.DurationSec;
            double bikePlanMeters = walk0.DistanceMeters + bike0.DistanceMeters + walkMid.DistanceMeters + bike1.DistanceMeters + walk2.DistanceMeters;


            var steps = new List<string>
            {
            $"Start point: {req.Origin}",
            $"End point: {req.Destination}",
            "The origin and destination are in different JCDecaux networks (inter-city).",
            $"Origin contract: {originContract}",
            $"Destination contract: {destContract}",
            $"Walk to station '{oPickup.Name}'.",
            $"Ride to station '{oDrop.Name}'.",
            $"Walk (inter-city) to station '{dPickup.Name}'.",
            $"Ride to station '{dDrop.Name}'.",
            "Walk to the destination."
            };


            return new ItineraryResponse
            {
                Success = true,
                Message = "MVP OK — both-ends bike",
                Instructions = steps.ToArray(),
                UseBike = bikePlanSec + 60 < walkOnly.DurationSec,
                OriginContract = originContract,
                DestContract = destContract,
                BikeFrom = oPickup.Name,
                BikeTo = dDrop.Name,
                WalkOnlyDurationSec = System.Math.Round(walkOnly.DurationSec, 1),
                BikePlanDurationSec = System.Math.Round(bikePlanSec, 1),
                Walk1Coords = walk0.Coords,
                BikeCoords = bike0.Coords,
                Walk2Coords = walk2.Coords,
                WalkOnlyDistanceMeters = System.Math.Round(walkOnly.DistanceMeters, 1),
                BikePlanDistanceMeters = System.Math.Round(bikePlanMeters, 1),
                Walk1DistanceMeters = System.Math.Round(walk0.DistanceMeters, 1),
                BikeDistanceMeters = System.Math.Round(bike0.DistanceMeters, 1),
                Walk2DistanceMeters = System.Math.Round(walk2.DistanceMeters, 1),
                Segments = new List<ItineraryResponse.RouteSegment>
                {
                Seg("walk", null, "Origin", oPickup.Name, walk0),
                Seg("bike", originContract, oPickup.Name, oDrop.Name, bike0),
                Seg("walk", null, oDrop.Name, dPickup.Name, walkMid),
                Seg("bike", destContract, dPickup.Name, dDrop.Name, bike1),
                Seg("walk", null, dDrop.Name, "Destination", walk2)
                }
            };
        }

        private static ItineraryResponse.RouteSegment Seg(string mode, string contract, string fromName, string toName, OrsRoute r)
        => new ItineraryResponse.RouteSegment
        {
            Mode = mode,
            Contract = contract,
            FromName = fromName,
            ToName = toName,
            Coords = r?.Coords ?? new List<double[]>(),
            DistanceMeters = r?.DistanceMeters ?? 0,
            DurationSec = r?.DurationSec ?? 0
        };
    }
}

