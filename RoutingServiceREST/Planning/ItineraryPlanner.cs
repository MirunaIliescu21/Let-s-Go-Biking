using System;
using System.Collections.Generic;
using System.Linq;
using RoutingServiceREST.Services;
using RoutingServiceREST.Utils;


namespace RoutingServiceREST.Planning
{
    public sealed class ItineraryPlanner : IItineraryPlanner
    {
        private readonly IRoutingCoreService _routing;
        private readonly PlannerOptions _opt;

        public ItineraryPlanner(IRoutingCoreService routing, PlannerOptions opt)
        {
            _routing = routing;
            _opt = opt;
        }

        public ItineraryResponse Plan(ItineraryRequest req, StationContext ctx)
        {
            bool interCity = !string.Equals(ctx.OriginContract, ctx.DestContract, StringComparison.OrdinalIgnoreCase);


            var baselineWalk = _routing.GetRoute(
                            "foot-walking",
                            new Services.LatLon(req.OriginLat, req.OriginLon),
                            new Services.LatLon(req.DestLat, req.DestLon));

            var candidates = new List<ItineraryResponse>();

            var planOrigin = OriginOnlyPlan.Build(_routing, req, ctx);
            var planDest = DestinationOnlyPlan.Build(_routing, req, ctx);
            var planBoth = BothEndsPlan.Build(_routing, req, ctx);


            void Consider(ItineraryResponse p)
            {
                if (p == null) return;
                if (p.BikePlanDurationSec + _opt.SmallBufferSec < baselineWalk.DurationSec) candidates.Add(p);
            }

            if (interCity)
            {
                Consider(planOrigin);
                Consider(planDest);
                if (planBoth != null)
                {
                    double bestSingle = Math.Min(planOrigin?.BikePlanDurationSec ?? double.MaxValue,
                    planDest?.BikePlanDurationSec ?? double.MaxValue);
                    if (planBoth.BikePlanDurationSec + _opt.BothEndsAdvantageSec < Math.Min(bestSingle, baselineWalk.DurationSec))
                        candidates.Add(planBoth);
                }
            }
            else
            {
                // same contract -> the same origin/dest strategies still work
                Consider(planOrigin);
            }

            if (candidates.Count == 0)
                return WalkOnlyPlan.FromRouting(_routing, req, interCity ?
                "The origin and destination are in different JCDecaux networks (inter-city)." :
                "Bike is not worthwhile for this route.");

            var best = candidates.OrderBy(c => c.BikePlanDurationSec).First();

            if (interCity)
            {
                const string interMsg = "The origin and destination are in different JCDecaux networks (inter-city).";

                // Insert only if not already present (BothEnds already includes it)
                bool alreadyHas = best.Instructions?.Any(s => string.Equals(s, interMsg, StringComparison.Ordinal)) == true;
                if (!alreadyHas)
                {
                    var steps = new List<string>(best.Instructions ?? Array.Empty<string>());
                    int insertAt = Math.Min(2, steps.Count);
                    steps.Insert(insertAt, interMsg);
                    best.Instructions = steps.ToArray();
                }
            }

            // Add summary message
            double walkMin = Math.Round(baselineWalk.DurationSec / 60.0);
            double bikeMin = Math.Round(best.BikePlanDurationSec / 60.0);
            double savedMin = Math.Max(0, walkMin - bikeMin);

            var prefix = interCity ? "[Inter-city] " : "";
            best.Message = $"{prefix}Bike plan selected: {bikeMin} min vs walking {walkMin} min (saves ~{savedMin} min).";


            return best;
        }
    }
}


//using System;
//using System.Collections.Generic;
//using System.Linq;
//using RoutingServiceREST.Services;
//using RoutingServiceREST.Utils;

//namespace RoutingServiceREST.Planning
//{
//    /// <summary>
//    /// Plans an itinerary choosing between walking-only and bike-assisted variants.
//    /// The planner:
//    /// 1. Computes a walk-only baseline using the routing service.
//    /// 2. Builds candidate bike plans (origin-only, destination-only, both-ends).
//    /// 3. Filters candidates by configured buffers and advantages.
//    /// 4. Returns the best bike plan or a walk-only plan with a reason.
//    /// </summary>
//    public sealed class ItineraryPlanner : IItineraryPlanner
//    {
//        private readonly IRoutingCoreService _routing;
//        private readonly PlannerOptions _opt;

//        public ItineraryPlanner(IRoutingCoreService routing, PlannerOptions opt)
//        {
//            _routing = routing ?? throw new ArgumentNullException(nameof(routing));
//            _opt = opt ?? throw new ArgumentNullException(nameof(opt));
//        }

//        /// <summary>
//        /// Plan an itinerary for the given request and station context.
//        /// </summary>
//        public ItineraryResponse Plan(ItineraryRequest req, StationContext ctx)
//        {
//            if (req == null) throw new ArgumentNullException(nameof(req));
//            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

//            // Whether origin and destination are in different JCDecaux networks
//            bool interCity = !string.Equals(ctx.OriginContract, ctx.DestContract, StringComparison.OrdinalIgnoreCase);

//            // 1) Baseline: walking-only route (used for comparisons / reasons)
//            var baselineWalk = _routing.GetRoute(
//                "foot-walking",
//                new LatLon(req.OriginLat, req.OriginLon),
//                new LatLon(req.DestLat, req.DestLon));

//            // 2) Build candidate bike plans
//            var planOrigin = OriginOnlyPlan.Build(_routing, req, ctx);
//            var planDest = DestinationOnlyPlan.Build(_routing, req, ctx);
//            var planBoth = BothEndsPlan.Build(_routing, req, ctx);

//            // Helper: add plan to candidates if it improves over walking by SmallBufferSec
//            void Consider(ItineraryResponse p, IList<ItineraryResponse> bag)
//            {
//                if (p == null) return;
//                if (p.BikePlanDurationSec + _opt.SmallBufferSec < baselineWalk.DurationSec)
//                    bag.Add(p);
//            }

//            // Helper: returns the best non-empty bike duration in seconds across provided plans
//            double? BestBikeSeconds(params ItineraryResponse[] plans)
//            {
//                if (plans == null)
//                    return null;

//                var best = plans.Where(p => p != null && p.BikePlanDurationSec > 0)
//                                .Select(p => (double?)p.BikePlanDurationSec)
//                                .ToList();
//                if (best.Count == 0) 
//                    return null;
//                return best.Min();
//            }

//            // Build a human-readable reason for choosing walking-only
//            string BuildWalkOnlyReason()
//            {
//                var bestBikeSec = BestBikeSeconds(planOrigin, planDest, planBoth);
//                if (bestBikeSec.HasValue && bestBikeSec.Value > 0)
//                {
//                    var walkMin = Math.Round(baselineWalk.DurationSec / 60.0);
//                    var bikeMin = Math.Round(bestBikeSec.Value / 60.0);
//                    var diff = Math.Abs(walkMin - bikeMin);
//                    return $"Walking is faster: {walkMin} min vs bike {bikeMin} min (Δ {diff} min).";
//                }

//                return interCity
//                    ? "The origin and destination are in different JCDecaux networks (inter-city)."
//                    : "Bike is not worthwhile for this route.";
//            }

//            // If inter-city, ensure the returned response contains a short informative note
//            void EnsureInterCityNote(ItineraryResponse resp)
//            {
//                if (!interCity || resp == null) return;

//                const string interMsg = "The origin and destination are in different JCDecaux networks (inter-city).";
//                bool alreadyHas = resp.Instructions?.Any(s => string.Equals(s, interMsg, StringComparison.Ordinal)) == true;
//                if (alreadyHas) return;

//                var steps = new List<string>(resp.Instructions ?? Array.Empty<string>());
//                int insertAt = Math.Min(2, steps.Count);
//                steps.Insert(insertAt, interMsg);
//                resp.Instructions = steps.ToArray();
//            }

//            // Append a short summary message to the chosen bike plan
//            void ApplySummaryMessage(ItineraryResponse resp)
//            {
//                if (resp == null) return;
//                double walkMin = Math.Round(baselineWalk.DurationSec / 60.0);
//                double bikeMin = Math.Round(resp.BikePlanDurationSec / 60.0);
//                double savedMin = Math.Max(0, walkMin - bikeMin);
//                var prefix = interCity ? "[Inter-city] " : "";
//                resp.Message = $"{prefix}Bike plan selected: {bikeMin} min vs walking {walkMin} min (saves ~{savedMin} min).";
//            }

//            // 3) Select candidates
//            var candidates = new List<ItineraryResponse>();
//            if (interCity)
//            {
//                Consider(planOrigin, candidates);
//                Consider(planDest, candidates);

//                if (planBoth != null)
//                {
//                    double bestSingle = Math.Min(
//                        planOrigin?.BikePlanDurationSec ?? double.MaxValue,
//                        planDest?.BikePlanDurationSec ?? double.MaxValue);

//                    if (planBoth.BikePlanDurationSec + _opt.BothEndsAdvantageSec
//                        < Math.Min(bestSingle, baselineWalk.DurationSec))
//                    {
//                        candidates.Add(planBoth);
//                    }
//                }
//            }
//            else
//            {
//                // same contract: origin-only strategy is usually sufficient
//                Consider(planOrigin, candidates);
//            }

//            // 4) If no suitable bike candidate -> return walk-only plan with a clear reason
//            if (candidates.Count == 0)
//                return WalkOnlyPlan.FromRouting(_routing, req, BuildWalkOnlyReason());

//            // 5) Choose the best candidate, annotate and return it
//            var bestItinerary = candidates.OrderBy(c => c.BikePlanDurationSec).First();
//            EnsureInterCityNote(bestItinerary);
//            ApplySummaryMessage(bestItinerary);
//            return bestItinerary;
//        }
//    }
//}