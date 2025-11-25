using System.Runtime.Serialization;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RoutingServiceREST
{
    [DataContract]
    public class JcDecauxContract
    {
        [DataMember(Name = "name")]
        [JsonProperty("name")]
        public string Name { get; set; }

        [DataMember(Name = "commercial_name")]
        [JsonProperty("commercial_name")]
        public string CommercialName { get; set; }

        [DataMember(Name = "cities")]
        [JsonProperty("cities")]
        public List<string> Cities { get; set; } = new List<string>();

        [DataMember(Name = "country_code")]
        [JsonProperty("country_code")]
        public string CountryCode { get; set; }
    }

    [DataContract]
    public class ItineraryRequest
    {
        [DataMember(Order = 1, IsRequired = true)] public string Origin { get; set; }
        [DataMember(Order = 2, IsRequired = true)] public string Destination { get; set; }
        [DataMember(Order = 3)] public double OriginLat { get; set; }
        [DataMember(Order = 4)] public double OriginLon { get; set; }
        [DataMember(Order = 5)] public double DestLat { get; set; }
        [DataMember(Order = 6)] public double DestLon { get; set; }
        [DataMember(Order = 99)] public bool Debug { get; set; }

    }

    [DataContract]
    public class ItineraryResponse
    {
        [DataMember(Order = 1)] public bool Success { get; set; }
        [DataMember(Order = 2)] public string Message { get; set; }
        [DataMember(Order = 3)] public string[] Instructions { get; set; }
        [DataMember(Order = 4)] public bool UseBike { get; set; }
        [DataMember(Order = 5)] public string OriginContract { get; set; }
        [DataMember(Order = 6)] public string DestContract { get; set; }
        [DataMember(Order = 7)] public string BikeFrom { get; set; }
        [DataMember(Order = 8)] public string BikeTo { get; set; }
        [DataMember(Order = 9)] public double WalkOnlyDurationSec { get; set; }
        [DataMember(Order = 10)] public double BikePlanDurationSec { get; set; }

        // Coordinates for mapping (lat,lon)
        [DataMember(Order = 11)] public List<double[]> Walk1Coords { get; set; }
        [DataMember(Order = 12)] public List<double[]> BikeCoords { get; set; }
        [DataMember(Order = 13)] public List<double[]> Walk2Coords { get; set; }

        // Distances in meters
        [DataMember(Order = 14)] public double WalkOnlyDistanceMeters { get; set; }
        [DataMember(Order = 15)] public double BikePlanDistanceMeters { get; set; }
        [DataMember(Order = 16)] public double Walk1DistanceMeters { get; set; }
        [DataMember(Order = 17)] public double BikeDistanceMeters { get; set; }
        [DataMember(Order = 18)] public double Walk2DistanceMeters { get; set; }

        [DataMember(Order = 19)]
        public List<RouteSegment> Segments { get; set; }

        // Debug info 
        [DataMember(Order = 90)] public double OriginResolvedLat { get; set; }
        [DataMember(Order = 91)] public double OriginResolvedLon { get; set; }
        [DataMember(Order = 92)] public double DestResolvedLat { get; set; }
        [DataMember(Order = 93)] public double DestResolvedLon { get; set; }

        [DataMember(Order = 94)] public List<DebugStationChoice> BikeFromTop3 { get; set; }
        [DataMember(Order = 95)] public List<DebugStationChoice> BikeToTop3 { get; set; }

        [DataContract]
        public class DebugStationChoice
        {
            [DataMember(Order = 1)] public string Name { get; set; }
            [DataMember(Order = 2)] public double Lat { get; set; }
            [DataMember(Order = 3)] public double Lon { get; set; }
            [DataMember(Order = 4)] public int Bikes { get; set; }
            [DataMember(Order = 5)] public int Stands { get; set; }
            [DataMember(Order = 6)] public double DistanceMeters { get; set; }
        }

        // Type for route segments (multiple bikes possible)
        [DataContract]
        public class RouteSegment
        {
            [DataMember(Order = 1)] public string Mode { get; set; }         // "walk" | "bike"
            [DataMember(Order = 2)] public string Contract { get; set; }     // null for walk
            [DataMember(Order = 3)] public string FromName { get; set; }     // station or "Origin"
            [DataMember(Order = 4)] public string ToName { get; set; }       // station or "Destination"
            [DataMember(Order = 5)] public List<double[]> Coords { get; set; } = new List<double[]>();
            [DataMember(Order = 6)] public double DistanceMeters { get; set; }
            [DataMember(Order = 7)] public double DurationSec { get; set; }
        }

    }

    // JCDecaux v3 models
    public class JcdecauxStation
    {
        [JsonProperty("name")] public string Name { get; set; }

        [JsonProperty("contractName")]
        public string ContractName { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }

        [JsonProperty("totalStands")]
        public TotalStands TotalStands { get; set; }
    }

    [DataContract]
    public class ContractQuery
    {
        [DataMember(Order = 1)]
        public string Contract { get; set; }
    }

    public class Position
    {
        [JsonProperty("latitude")]
        public double Lat { get; set; }

        [JsonProperty("longitude")]
        public double Lon { get; set; }
    }

    public class TotalStands
    {
        [JsonProperty("availabilities")]
        public Availabilities Availabilities { get; set; }
    }

    public class Availabilities
    {
        [JsonProperty("bikes")] public int Bikes { get; set; }
        [JsonProperty("stands")] public int Stands { get; set; }
    }
}

