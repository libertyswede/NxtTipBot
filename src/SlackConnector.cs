using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NxtTipBot
{
    public class SlackConnector
    {
        private readonly string apiToken;
        private List<Channel> channels;
        private List<User> users;
        private List<InstantMessage> instantMessages; 

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
                var jObject = JObject.Parse(json);
                websocketUri = (string)jObject["url"];
                channels = JsonConvert.DeserializeObject<List<Channel>>(jObject["channels"].ToString());
                users = JsonConvert.DeserializeObject<List<User>>(jObject["users"].ToString());
                instantMessages = JsonConvert.DeserializeObject<List<InstantMessage>>(jObject["ims"].ToString());
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
                        case "message": HandleMessage(json);
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

        private void HandleMessage(string json)
        {
            var message = JsonConvert.DeserializeObject<Message>(json);
            var user = users.Single(u => u.Id == message.User);
            var channel = channels.SingleOrDefault(c => c.Id == message.Channel);
            if (channel != null)
            {
                Console.Write($"#{channel.Name} ");
            }
            Console.WriteLine($"{user.Name}: {message.Text}");
        }
    }
}