using System.Collections.Generic;
using UnityEngine;

namespace LaundryNDishes.Core
{
    
    public static class TelemetrySender
    {
        public static bool RequestDelete(string installationId)
        {
            try
            {
                var request = System.Net.WebRequest.Create("https://your-endpoint/telemetry/delete");
                request.Method = "POST";
                request.ContentType = "application/json";

                string payload = JsonUtility.ToJson(new
                {
                    installationId
                });

                using (var stream = request.GetRequestStream())
                using (var writer = new System.IO.StreamWriter(stream))
                {
                    writer.Write(payload);
                }

                using var response = request.GetResponse();
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static void SendBatch(List<TelemetryEvent> events)
        {
            foreach (var ev in events)
            {
                Send(ev);
            }
        }

        public static void Send(TelemetryEvent ev)
        {
            string json = JsonUtility.ToJson(ev);

            // HTTP request aqui (UnityWebRequest precisa rodar corretamente em thread-safe pattern)
            Post(json);
        }

        private static void Post(string json)
        {
            var request = System.Net.WebRequest.Create("https://your-endpoint");
            request.Method = "POST";
            request.ContentType = "application/json";

            using (var stream = request.GetRequestStream())
            using (var writer = new System.IO.StreamWriter(stream))
            {
                writer.Write(json);
            }

            request.GetResponse().Close();
        }
    }
}