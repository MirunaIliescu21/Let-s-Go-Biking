using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace RoutingServiceREST
{
    [ServiceContract]
    public interface IRoutingServiceREST
    {
        // MVP: POST /itinerary (JSON in/out)
        [OperationContract]
        [WebInvoke(
            Method = "POST",
            UriTemplate = "/itinerary",
            RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare)]

        ItineraryResponse GetItinerary(ItineraryRequest request);

        // GET /ping (for health check)
        [OperationContract]
        [WebGet(UriTemplate = "/ping", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare)]
        string Ping();

        // GET /contracts  (list of contracts)
        [OperationContract]
        [WebGet(UriTemplate = "/contracts", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare)]
        List<JcDecauxContract> GetContracts();

        // GET /stations?contract={contract} (list of stations for a given contract)
        [OperationContract]
        [WebGet(UriTemplate = "/stations?contract={contract}", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare)]
        List<JcdecauxStation> GetStations(string contract);

        [OperationContract]
        [WebInvoke(Method = "OPTIONS", UriTemplate = "*")]
        void Options();
    }
}
