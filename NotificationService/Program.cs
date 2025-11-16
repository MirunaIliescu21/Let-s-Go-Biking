using System;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Newtonsoft.Json;

namespace NotificationService
{
    /**
     * NotificationService simulates weather/pollution/bike events, 
     * and the frontend projects them on the map of the current route:
     *   weather near the origin,
     *   pollution near the destination,
     *   bike problems near the stations used in the itinerary.
     */
    internal class Program
    {
        private static readonly Random _rand = new Random();

        static void Main(string[] args)
        {
            // Conectare la ActiveMQ local
            var factory = new ConnectionFactory("tcp://localhost:61616");

            using (var connection = factory.CreateConnection("admin", "admin"))
            {
                connection.Start();
                using (var session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge))
                {
                    // Topic-uri JMS
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

                            Thread.Sleep(5000); // un eveniment la 5 secunde
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
                Lat = 43.604652,  // Toulouse
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
                Lat = 45.764043,  // Lyon
                Lon = 4.835659,
                Timestamp = DateTime.UtcNow
            };

            SendEvent(session, producer, evt);
        }

        private static void SendBikeEvent(ISession session, IMessageProducer producer)
        {
            var evt = new NotificationEvent
            {
                Type = "bikes",
                Severity = RandomSeverity(),
                Message = "Low number of bikes at the closest station, consider an alternative.",
                Lat = null,
                Lon = null,
                Timestamp = DateTime.UtcNow
            };

            SendEvent(session, producer, evt);
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
