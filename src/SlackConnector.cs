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

namespace NxtTipbot
{
    public class SlackConnector
    {
        private readonly string apiToken;
        private readonly ILogger logger;
        private readonly SlackHandler slackHandler;

        private string selfId;
        private List<Channel> channels;
        private List<User> users;
        private List<InstantMessage> instantMessages;
        private ClientWebSocket webSocket;
        private readonly UTF8Encoding encoder = new UTF8Encoding();

        public SlackConnector(string apiToken, ILogger logger, SlackHandler slackHandler)
        {
            this.logger = logger;
            this.apiToken = apiToken;
            this.slackHandler = slackHandler;
        }

        public async Task Run()
        {
            string websocketUri = string.Empty;
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync($"https://slack.com/api/rtm.start?token={apiToken}"))
            using (var content = response.Content)
            {
                var json = await content.ReadAsStringAsync();
                logger.LogTrace($"Initial handshake reply with rtm.start: {json}");
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
                    logger.LogInformation("MessageType.Close recieved, closing connection to Slack.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else
                {
                    var json = encoder.GetString(buffer, 0, result.Count);
                    var jObject = JObject.Parse(json);
                    var type = (string)jObject["type"];
                    switch (type)
                    {
                        case "presence_change":// ignore these
                        case "reconnect_url":
                        case "user_typing":
                        case "hello":
                            break;

                        case "message": await HandleMessage(json);
                            break;
                        
                        case "im_created": HandleIMCreated(jObject);
                            break;
                        case "channel_created": HandleChannelCreated(jObject);
                            break;
                        
                        case null: HandleNullType(jObject, json);
                            break;
                        default: logger.LogTrace($"Data recieved: {json}");
                            break; 
                    }
                }
            }
        }

        private async Task HandleMessage(string json)
        {
            var message = JsonConvert.DeserializeObject<Message>(json);
            var user = users.SingleOrDefault(u => u.Id == message.UserId);
            var channel = channels.SingleOrDefault(c => c.Id == message.ChannelId);
            var instantMessage = instantMessages.SingleOrDefault(im => im.Id == message.ChannelId);
            
            if (user != null && user.Id != selfId)
            {
                if (channel != null && message.Text.StartsWith("tipbot"))
                {
                    await slackHandler.HandleTipBotChannelCommand(message, user, channel);
                }
                else if (instantMessage != null)
                {
                    await slackHandler.InstantMessageRecieved(message, user, instantMessage);
                }
            }
        }

        private void HandleIMCreated(JObject jObject)
        {
            var instantMessage = new InstantMessage
            {
                Id = (string)jObject["channel"]["id"],
                UserId = (string)jObject["user"]
            };
            instantMessages.Add(instantMessage);

            var user = users.Single(u => u.Id == instantMessage.UserId);
            logger.LogTrace($"IM with user {user.Name} was created.");
        }

        private void HandleChannelCreated(JObject jObject)
        {
            var channel = JsonConvert.DeserializeObject<Channel>(jObject["channel"].ToString());
            channels.Add(channel);
            logger.LogTrace($"#{channel.Name} was created.");
        }

        private void HandleNullType(JObject jObject, string json)
        {
            if ((string)jObject["reply_to"] == null) 
                logger.LogTrace(json);
        }

        public async Task SendMessage(string channelId, string message, bool unfurl_links = true)
        {
            logger.LogTrace($"Sending chat.postMessage to channel id: {channelId}, message: {message}, unfurl_links: {unfurl_links}");
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync($"https://slack.com/api/chat.postMessage?token={apiToken}&channel={channelId}&text={message}&unfurl_links={unfurl_links}"))
            using (var content = response.Content)
            {
                var json = await content.ReadAsStringAsync();
                logger.LogTrace($"Reply from request to chat.postMessage: {json}");
            }
        }

        public async Task<string> GetInstantMessageId(string userId)
        {
            var id = instantMessages.SingleOrDefault(im => im.UserId == userId)?.Id;
            if (id == null)
            {
                logger.LogTrace($"Requesting im.open with user {userId}");
                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync($"https://slack.com/api/im.open?token={apiToken}&user={userId}"))
                using (var content = response.Content)
                {
                    var json = await content.ReadAsStringAsync();
                    logger.LogTrace($"Reply from request to im.open: {json}");
                    var jObject = JObject.Parse(json);
                    id = (string)jObject["channel"]["id"];
                    instantMessages.Add(new InstantMessage {Id = id, UserId = userId});
                }
            }
            return id;
        }
    }
}