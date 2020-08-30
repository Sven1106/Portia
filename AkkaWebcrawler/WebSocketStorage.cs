using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AkkaWebcrawler
{
    public static class WebSocketStorage // TODO How to handle this concurrent between actors?
    {
        private static Dictionary<Guid, string> _webSocketConnectionByProjectId;
        private static readonly string _filename = "websocketConnectionsByProjectId.json";
        static WebSocketStorage()
        {
            if (File.Exists(_filename))
            {
                string textFromFile = File.ReadAllText(_filename);
                _webSocketConnectionByProjectId = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(textFromFile);
            }
            else
            {
                _webSocketConnectionByProjectId = new Dictionary<Guid, string>();
            }
        }

        public static string GetWebSocket(Guid projectId)
        {
            string webSocket = null;
            if (_webSocketConnectionByProjectId.ContainsKey(projectId))
            {
                webSocket = _webSocketConnectionByProjectId[projectId];
            }
            return webSocket;
        }
        public static void RemoveWebSocket(Guid projectId)
        {
            _webSocketConnectionByProjectId.Remove(projectId);
            SaveWebSockets();
        }

        public static void AddWebSocket(Guid projectId, string webSocketConnection)
        {
            _webSocketConnectionByProjectId.Add(projectId, webSocketConnection);
            SaveWebSockets();
        }

        private static void SaveWebSockets()
        {
            File.WriteAllText(_filename, JsonConvert.SerializeObject(_webSocketConnectionByProjectId));
        }
    }
}
