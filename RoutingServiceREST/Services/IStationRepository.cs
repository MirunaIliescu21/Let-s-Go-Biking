using System.Collections.Generic;

namespace RoutingServiceREST.Services
{
    public interface IStationRepository
    {
        List<JcdecauxStation> GetAllStations();
    }
}