using System;

namespace LaundryNDishes.Core
{
    public enum TelemetryState
    {
        Pending,
        Sent,
        DoNotSend 
    }
    [System.Serializable]
    public class TelemetryEvent
    {
        public string id;
        public string installationId;
        public string unityVersion;
        public string pluginVersion;
        public string action;
        public string mode;
        public string taget;
        public long timestamp;

        public TelemetryState state;
    }
}
/*
TelemetryQueue.Enqueue(new TelemetryEvent
   {
       installationId = LnDConfig.instance.InstallationId,
       unityVersion = Application.unityVersion,
       pluginVersion = "1.0.0",
       action = "GenerateTests",
       mode = "Remote",
       timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
   });
*/