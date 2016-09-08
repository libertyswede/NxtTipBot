using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NxtTipBot
{
    public class SlackConnector
    {
        private readonly string apiToken;

        public SlackConnector(string apiToken)
        {
            this.apiToken = apiToken;
        }

        public async Task Run()
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
                        case "hello": Console.WriteLine("Hello recieved.");
                            break;
                        case "presence_change": // ignore
                        case "reconnect_url":
                        case "user_typing":
                            break;
                        default: Console.WriteLine(json);
                            break; 
                    }
                }
            }
        }
    }
}