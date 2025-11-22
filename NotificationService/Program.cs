using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Newtonsoft.Json;
using NotificationService.ProxyCache;
using System;
using System.Configuration;
using System.Threading;
using System.Xml.Linq;


namespace NotificationService
{
    /**
     * NotificationService is an external real-time alert generator used by the project.
     *
     * It periodically produces three types of events:
     *   - METEO: short-term weather alerts, displayed near the route origin
     *   - POLLUTION: air-quality alerts, displayed near the route destination
     *   - BIKES: bike availability alerts based on real JCDecaux station data
     */
    internal class Program
    {
        private static readonly Random _rand = new Random();
        private static string _alertContract;


        static void Main(string[] args)
        {
            // Connect to the ActiveMQ broker
            var factory = new ConnectionFactory("tcp://localhost:61616");

            using (var connection = factory.CreateConnection("admin", "admin"))
            {
                connection.Start();
                using (var session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge))
                {
                    // 3 topics for the 3 types of notifications
                    IDestination meteoTopic = session.GetTopic("meteo");
                    IDestination pollutionTopic = session.GetTopic("pollution");
                    IDestination bikesTopic = session.GetTopic("bikes");

                    using (var meteoProducer = session.CreateProducer(meteoTopic))
                    using (var pollutionProducer = session.CreateProducer(pollutionTopic))
                    using (var bikesProducer = session.CreateProducer(bikesTopic))
                    {
                        meteoProducer.DeliveryMode = MsgDeliveryMode.NonPersistent;
                        pollutionProducer.DeliveryMode = MsgDeliveryMode.NonPersistent;
                        bikesProducer.DeliveryMode = MsgDeliveryMode.NonPersistent;

                        Console.WriteLine("NotificationService running. Press Ctrl+C to stop.");
                        _alertContract = ConfigurationManager.AppSettings["ALERT_CONTRACT"] ?? "toulouse";
                        Console.WriteLine($"[NotificationService] Using JCDecaux contract for bike alerts: {_alertContract}");

                        while (true)
                        {
                            int choice = _rand.Next(3);
                            switch (choice)
                            {
                                case 0:
                                    SendMeteoEvent(session, meteoProducer);
                                    break;
                                case 1:
                                    SendPollutionEvent(session, pollutionProducer);
                                    break;
                                case 2:
                                    SendBikeEvent(session, bikesProducer);
                                    break;
                            }

                            Thread.Sleep(5000); // an event every 5 seconds
                        }
                    }
                }
            }
        }

        private static void SendMeteoEvent(ISession session, IMessageProducer producer)
        {
            var evt = new NotificationEvent
            {
                Type = "meteo",
                Severity = RandomSeverity(),
                Message = "Short-term rain expected on your route.",
                Lat = 43.604652,  // Toulouse (only for initial testing without associating with the current itinerary)
                Lon = 1.444209,
                Timestamp = DateTime.UtcNow
            };

            SendEvent(session, producer, evt);
        }

        private static void SendPollutionEvent(ISession session, IMessageProducer producer)
        {
            var evt = new NotificationEvent
            {
                Type = "pollution",
                Severity = RandomSeverity(),
                Message = "High pollution level detected downtown.",
                Lat = 45.764043,  // Lyon (only for initial testing without associating with the current itinerary)
                Lon = 4.835659,
                Timestamp = DateTime.UtcNow
            };

            SendEvent(session, producer, evt);
        }

        /// <summary>
        /// Sends a "bikes" notification event to ActiveMQ.
        /// The event is based on *real* JCDecaux station data for the configured ALERT_CONTRACT.
        /// It calls the ProxyCacheService over SOAP, deserializes the JCDecaux JSON,
        /// randomly picks one station, and builds a message + optional coordinates.
        /// </summary>
        private static void SendBikeEvent(ISession session, IMessageProducer producer)
        {
            try
            {
                // 1. Create a SOAP client to talk to the ProxyCacheService.
                //    ProxyServiceClient is generated from the WSDL (Service Reference "ProxyCache").
                var client = new ProxyServiceClient();

                // 2. Fetch the stations JSON for the selected JCDecaux contract.
                //    The contract name comes from App.config (ALERT_CONTRACT, by default is "toulouse").
                //    The proxy/cache service handles TTL and caching.
                string json = client.GetJcdecauxStationsGeneric(_alertContract, 30);

                // 3. Parse the JSON into a dynamic object
                dynamic stations = JsonConvert.DeserializeObject(json);

                // If something is wrong or there are no stations, send a generic warning and exit.
                if (stations == null || stations.Count == 0)
                {
                    var fallback = new NotificationEvent
                    {
                        Type = "bikes",
                        Severity = RandomSeverity(),
                        Message = "Bike alerts unavailable (no stations found).",
                        Lat = null,
                        Lon = null,
                        Timestamp = DateTime.UtcNow
                    };

                    SendEvent(session, producer, fallback);
                    return;
                }

                // 4. Randomly pick a station from the list for this alert.
                int idx = _rand.Next((int)stations.Count);
                dynamic station = stations[idx];

                // 5. Extract availability information (bikes and stands) and the station name.
                int bikes = (int)station.totalStands.availabilities.bikes;
                int stands = (int)station.totalStands.availabilities.stands;
                string name = (string)station.name;

                // 6. Compute severity based on how many bikes are available.
                string severity;
                if (bikes < 3)
                    severity = "danger";
                else if (bikes < 5)
                    severity = "warning";
                else
                    severity = "info";

                string message = $"Station '{name}' has {bikes} bikes and {stands} free stands.";

                // 7. Try to extract the station coordinates safely.
                //    Different JCDecaux feeds may use either (lat,lng) or (latitude,longitude).
                double? lat = null;
                double? lon = null;

                try
                {
                    if (station.position != null && station.position.latitude != null && station.position.longitude != null)
                    {
                        lat = (double)station.position.latitude;
                        lon = (double)station.position.longitude;
                    }
                }
                catch
                {
                    // If anything goes wrong while parsing coordinates,
                    // leave lat/lon as null and let the front-end use its fallback logic.
                }

                // 8. Build the NotificationEvent using real JCDecaux data.
                var evt = new NotificationEvent
                {
                    Type = "bikes",
                    Severity = severity,
                    Message = message,
                    Lat = lat,
                    Lon = lon,
                    Timestamp = DateTime.UtcNow
                };

                // For Debug
                Console.WriteLine($"[DEBUG BIKES] {name} => lat={lat?.ToString() ?? "null"}, lon={lon?.ToString() ?? "null"}");

                // 9. Serialize the event as JSON and send it to the "bikes" topic via ActiveMQ.
                SendEvent(session, producer, evt);
            }
            catch (Exception ex)
            {
                // If the SOAP call or JSON parsing fails, we still send a warning-level alert
                // so that the UI shows that the bike alert service is experiencing issues.
                var evt = new NotificationEvent
                {
                    Type = "bikes",
                    Severity = "warning",
                    Message = "Bike alert service error: " + ex.Message,
                    Lat = null,
                    Lon = null,
                    Timestamp = DateTime.UtcNow
                };

                SendEvent(session, producer, evt);
            }
        }

        private static void SendEvent(ISession session, IMessageProducer producer, NotificationEvent evt)
        {
            string json = JsonConvert.SerializeObject(evt);
            ITextMessage msg = session.CreateTextMessage(json);
            producer.Send(msg);
            Console.WriteLine($"[{evt.Type.ToUpper()}] {evt.Severity} - {evt.Message}");
        }

        private static string RandomSeverity()
        {
            string[] levels = { "info", "warning", "danger" };
            return levels[_rand.Next(levels.Length)];
        }
    }
}
