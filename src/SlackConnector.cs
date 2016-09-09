using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NxtTipBot
{
    public class SlackConnector
    {
        private readonly string apiToken;
        private readonly ILogger logger;
        private string selfId;
        private List<Channel> channels;
        private List<User> users;
        private List<InstantMessage> instantMessages;
        private ClientWebSocket webSocket;
        private readonly UTF8Encoding encoder = new UTF8Encoding();
        private int id = 1;

        public SlackConnector(string apiToken, ILogger logger)
        {
            this.logger = logger;
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
                selfId = (string)jObject["self"]["id"];
                channels = JsonConvert.DeserializeObject<List<Channel>>(jObject["channels"].ToString());
                users = JsonConvert.DeserializeObject<List<User>>(jObject["users"].ToString());
                instantMessages = JsonConvert.DeserializeObject<List<InstantMessage>>(jObject["ims"].ToString());
            }

            webSocket = new ClientWebSocket();            
            await webSocket.ConnectAsync(new System.Uri(websocketUri), CancellationToken.None);
            await Recieve();
        }

        private async Task Recieve()
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
                    var json = encoder.GetString(buffer, 0, result.Count);
                    var jObject = JObject.Parse(json);
                    var type = (string)jObject["type"];
                    switch (type)
                    {
                        case "channel_created": HandleChannelCreated(jObject);
                            break;
                        case "hello": logger.LogDebug("Hello recieved.");
                            break;
                        case "message": await HandleMessage(json);
                            break;
                        case "channel_archive": // ignore
                        case "presence_change":
                        case "reconnect_url":
                        case "user_typing":
                            break;
                        case null: if ((string)jObject["reply_to"] != "1") logger.LogDebug(json);
                            break;
                        default: logger.LogDebug(json);
                            break; 
                    }
                }
            }
        }

        private void HandleChannelCreated(JObject jObject)
        {
            var channel = JsonConvert.DeserializeObject<Channel>(jObject["channel"].ToString());
            channels.Add(channel);
            logger.LogTrace($"#{channel.Name} was created.");
        }

        private async Task HandleMessage(string json)
        {
            var message = JsonConvert.DeserializeObject<Message>(json);
            var user = users.Single(u => u.Id == message.User);
            var channel = channels.SingleOrDefault(c => c.Id == message.Channel);
            var instantMessage = instantMessages.SingleOrDefault(im => im.Id == message.Channel);
            if (channel != null)
            {
                logger.LogDebug($"#{channel.Name} {user.Name}: {message.Text}");
            }
            else if (instantMessage != null)
            {
                logger.LogDebug($"{user.Name}: {message.Text}");
            }
            
            if (user.Id != selfId)
            {
                if (instantMessage != null)
                {
                    await SendMessage(instantMessage.Id, "Got it!");
                }
                else if (channel != null)
                {
                    await SendMessage(channel.Id, "Stop spamming!");
                }
            }
        }

        private async Task SendMessage(string channel, string message)
        {
            var obj = new {id = id++, type = "message", channel = channel, text = message};
            var json = JsonConvert.SerializeObject(obj);
            var buffer = encoder.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}