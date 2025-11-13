namespace RoutingServiceREST.Planning
{
    public sealed class PlannerOptions
    {
        public double SmallBufferSec { get; set; } = 60; // lock/unlock etc.
        public double BothEndsAdvantageSec { get; set; } = 120; // stricter than single-side
    }
}
