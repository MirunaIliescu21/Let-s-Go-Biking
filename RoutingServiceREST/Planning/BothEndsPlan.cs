using System.Linq;
using RoutingServiceREST.Services;
using RoutingServiceREST.Utils;


namespace RoutingServiceREST.Planning
{
    public static class BothEndsPlan
    {
        public static ItineraryResponse Build(IRoutingCoreService routing, ItineraryRequest req, StationContext ctx)
        {
            var originStations = ctx.All.Where(s => s.ContractName.Equals(ctx.OriginContract, System.StringComparison.OrdinalIgnoreCase)).ToList();
            var destStations = ctx.All.Where(s => s.ContractName.Equals(ctx.DestContract, System.StringComparison.OrdinalIgnoreCase)).ToList();


            var oPickup = StationPickers.ClosestWithBikes(originStations, req.OriginLat, req.OriginLon);
            var oDrop = StationPickers.WithStandsNearestTo(originStations, req.DestLat, req.DestLon);
            var dPickup = StationPickers.WithBikesNearestTo(destStations, req.OriginLat, req.OriginLon);
            var dDrop = StationPickers.WithStandsNearestTo(destStations, req.DestLat, req.DestLon);
            if (oPickup == null || oDrop == null || dPickup == null || dDrop == null) return null;


            var walk0 = routing.GetRoute("foot-walking", new LatLon(req.OriginLat, req.OriginLon), new LatLon(oPickup.Position.Lat, oPickup.Position.Lon));
            var bike0 = routing.GetRoute("cycling-regular", new LatLon(oPickup.Position.Lat, oPickup.Position.Lon), new LatLon(oDrop.Position.Lat, oDrop.Position.Lon));
            var walkMid = routing.GetRoute("foot-walking", new LatLon(oDrop.Position.Lat, oDrop.Position.Lon), new LatLon(dPickup.Position.Lat, dPickup.Position.Lon));
            var bike1 = routing.GetRoute("cycling-regular", new LatLon(dPickup.Position.Lat, dPickup.Position.Lon), new LatLon(dDrop.Position.Lat, dDrop.Position.Lon));
            var walk2 = routing.GetRoute("foot-walking", new LatLon(dDrop.Position.Lat, dDrop.Position.Lon), new LatLon(req.DestLat, req.DestLon));


            return ResponseBuilders.BothEnds(req, ctx.OriginContract, ctx.DestContract,
            oPickup, oDrop, dPickup, dDrop, walk0, bike0, walkMid, bike1, walk2, routing);
        }
    }
}