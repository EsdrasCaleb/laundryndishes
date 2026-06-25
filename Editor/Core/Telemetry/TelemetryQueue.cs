using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LaundryNDishes.Core
{
    public static class TelemetryQueue
    {
        private static string PathFile =>
            Path.Combine(Application.dataPath, "../ProjectSettings/LnDTelemetryQueue.json");

        private static List<TelemetryEvent> _cache;
        public static bool HasPendingData()
        {
            return Load().Count > 0;
        }

        
        private static List<TelemetryEvent> Load()
        {
            if (_cache != null) return _cache;

            if (!File.Exists(PathFile))
                _cache = new List<TelemetryEvent>();
            else
                _cache = JsonUtility.FromJson<TelemetryWrapper>(File.ReadAllText(PathFile)).items;

            return _cache;
        }

        public static void Enqueue(string action, string mode, string taget)
        {

            var list = Load();

            var ev = new TelemetryEvent
            {
                id = System.Guid.NewGuid().ToString("N"),
                installationId = LnDConfig.instance.InstallationId,
                unityVersion = Application.unityVersion,
                pluginVersion = "1.0.0",
                action = action,
                mode = mode,
                taget = taget,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                state = TelemetryState.Pending
            };

            list.Add(ev);
            Save(list);
        }

        public static List<TelemetryEvent> GetPending()
        {
            return Load().FindAll(e => e.state == TelemetryState.Pending);
        }
        
        public static void MarkAsSent(List<TelemetryEvent> sent)
        {
            var list = Load();

            foreach (var ev in sent)
            {
                var item = list.Find(x => x.id == ev.id);
                if (item != null)
                    item.state = TelemetryState.Sent;
            }

            Save(list);
        }
        
        public static void MarkAllAsDoNotSend()
        {
            var list = Load();

            foreach (var ev in list)
            {
                ev.state = TelemetryState.DoNotSend;
            }

            Save(list);
        }
        
        public static List<TelemetryEvent> GetReenableData()
        {
            return Load().FindAll(e =>
                e.state == TelemetryState.Pending ||
                e.state == TelemetryState.DoNotSend
            );
        }

        private static void Save(List<TelemetryEvent> list)
        {
            var wrapper = new TelemetryWrapper { items = list };
            File.WriteAllText(PathFile, JsonUtility.ToJson(wrapper, true));
        }

        [System.Serializable]
        private class TelemetryWrapper
        {
            public List<TelemetryEvent> items;
        }
    }
}