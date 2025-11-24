namespace RoutingServiceREST.Planning
{
    public sealed class PlannerOptions
    {
        // Small Buffer is a small safety margin, 60 seconds, that any bike plan has to beat compared to walking
        public double SmallBufferSec { get; set; } = 60;
        // Both Ends Advantage is the minimum advantage that a both-ends bike plan contains over the best single-bike plan
        public double BothEndsAdvantageSec { get; set; } = 120; 
    }
}
