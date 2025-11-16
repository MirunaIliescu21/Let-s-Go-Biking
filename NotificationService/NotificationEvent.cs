using System;

namespace NotificationService
{
    public class NotificationEvent
    {
        public string Type { get; set; }          // "meteo" | "pollution" | "bikes"
        public string Severity { get; set; }      // "info" | "warning" | "danger"
        public string Message { get; set; }
        public double? Lat { get; set; }          // affected zone
        public double? Lon { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
