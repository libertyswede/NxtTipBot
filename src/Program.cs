using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json.Linq;

namespace NxtTipBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configSettings = ReadConfig();
            var apiToken = configSettings.Single(c => c.Key == "apitoken").Value;
            var program = new Program();
            Task.Run(() => program.Run(apiToken)).Wait();
            Console.WriteLine("Done!");
        }

        private static IEnumerable<IConfigurationSection> ReadConfig()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(PlatformServices.Default.Application.ApplicationBasePath);
            configBuilder.AddJsonFile("config.json");
            configBuilder.AddJsonFile("config-Development.json", true);
            var configRoot = configBuilder.Build();
            var configSettings = configRoot.GetChildren();
            return configSettings;
        }

        private async Task Run(string apiToken)
        {
            string websocketUri = string.Empty;
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync($"https://slack.com/api/rtm.start?token={apiToken}"))
            using (var content = response.Content)
            {
                var json = await content.ReadAsStringAsync();
                // Console.WriteLine(json);
                var jObject = JObject.Parse(json);
                websocketUri = (string)jObject["url"];
            }

            var webSocket = new ClientWebSocket();            
            await webSocket.ConnectAsync(new System.Uri(websocketUri), CancellationToken.None);
            await Recieve(webSocket);
        }

        private async Task Recieve(ClientWebSocket webSocket)
        {
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    var encoder = new UTF8Encoding();
                    var json = encoder.GetString(buffer, 0, result.Count);
                    var jObject = JObject.Parse(json);
                    var type = (string)jObject["type"];
                    switch (type)
                    {
                        case "hello": // ignore
                            break;
                        case "reconnect_url": // ignore
                            break;
                        default: Console.WriteLine(json);
                            break; 
                    }
                    
                }
            }
        }
    }
}
