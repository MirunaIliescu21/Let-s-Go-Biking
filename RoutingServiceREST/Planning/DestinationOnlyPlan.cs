using System.Linq;
using RoutingServiceREST.Services;
using RoutingServiceREST.Utils;


namespace RoutingServiceREST.Planning
{
    public static class DestinationOnlyPlan
    {
        public static ItineraryResponse Build(IRoutingCoreService routing, ItineraryRequest req, StationContext ctx)
        {
            var destStations = ctx.All.Where(s => s.ContractName.Equals(ctx.DestContract, System.StringComparison.OrdinalIgnoreCase)).ToList();
            var pickup = StationPickers.WithBikesNearestTo(destStations, req.OriginLat, req.OriginLon);
            var drop = StationPickers.WithStandsNearestTo(destStations, req.DestLat, req.DestLon);
            if (pickup == null || drop == null) return null;


            var walk1 = routing.GetRoute("foot-walking", new LatLon(req.OriginLat, req.OriginLon), new LatLon(pickup.Position.Lat, pickup.Position.Lon));
            var bike = routing.GetRoute("cycling-regular", new LatLon(pickup.Position.Lat, pickup.Position.Lon), new LatLon(drop.Position.Lat, drop.Position.Lon));
            var walk2 = routing.GetRoute("foot-walking", new LatLon(drop.Position.Lat, drop.Position.Lon), new LatLon(req.DestLat, req.DestLon));


            return ResponseBuilders.DestinationOnly(req, ctx.DestContract, pickup, drop, walk1, bike, walk2, routing);
        }
    }
}
