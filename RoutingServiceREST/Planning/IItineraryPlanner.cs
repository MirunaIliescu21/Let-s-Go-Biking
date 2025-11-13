namespace RoutingServiceREST.Planning
{
    public interface IItineraryPlanner
    {
        ItineraryResponse Plan(ItineraryRequest req, StationContext ctx);
    }
}
