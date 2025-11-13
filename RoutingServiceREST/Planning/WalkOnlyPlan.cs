using RoutingServiceREST.Services;

namespace RoutingServiceREST.Planning
{
    public static class WalkOnlyPlan
    {
        public static ItineraryResponse FromRouting(IRoutingCoreService routing, ItineraryRequest req, string reason)
        {
            var walk = routing.GetRoute("foot-walking",
                new LatLon(req.OriginLat, req.OriginLon),
                new LatLon(req.DestLat, req.DestLon));
            return new ItineraryResponse
            {
                Success = true,
                // If reason is null or empty, it means everything is OK
                Message = string.IsNullOrWhiteSpace(reason) ? "MVP complet OK" : reason,
                Instructions = new[]
                {
                    $"Start point: {req.Origin}",
                    $"End point: {req.Destination}",
                    string.IsNullOrWhiteSpace(reason) ? "There is no useful JCDecaux network in the area." : reason,
                    "Walk to the destination (bike is not feasible in this scenario)."
                },
                UseBike = false,
                WalkOnlyDurationSec = System.Math.Round(walk.DurationSec, 1),
                WalkOnlyDistanceMeters = System.Math.Round(walk.DistanceMeters, 1),
                Walk1Coords = walk.Coords,
                Walk1DistanceMeters = System.Math.Round(walk.DistanceMeters, 1)
            };
        }


        public static ItineraryResponse FromRouting(IRoutingCoreService routing, ItineraryRequest req)
        => FromRouting(routing, req, reason: null);
    }
}
