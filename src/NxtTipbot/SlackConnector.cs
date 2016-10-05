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
    public interface ISlackConnector
    {
        Task SendMessage(string channelId, string message, bool unfurl_links = true);
        Task<string> GetInstantMessageId(string userId);
    }

    public class SlackConnector : ISlackConnector
    {
        private readonly string apiToken;
        private readonly ILogger logger;
        private readonly ISlackHandler slackHandler;

        private string selfId;
        private List<SlackChannelSession> channelSessions;
        private List<SlackUser> slackUsers;
        private List<SlackIMSession> imSessions;
        private ClientWebSocket webSocket;
        private readonly UTF8Encoding encoder = new UTF8Encoding();

        public SlackConnector(string apiToken, ILogger logger, ISlackHandler slackHandler)
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
                channelSessions = JsonConvert.DeserializeObject<List<SlackChannelSession>>(jObject["channels"].ToString());
                slackUsers = JsonConvert.DeserializeObject<List<SlackUser>>(jObject["users"].ToString());
                imSessions = JsonConvert.DeserializeObject<List<SlackIMSession>>(jObject["ims"].ToString());
            }

            webSocket = new ClientWebSocket();            
            await webSocket.ConnectAsync(new Uri(websocketUri), CancellationToken.None);
            await Recieve();
        }

        private async Task Recieve()
        {
            var buffer = new byte[16384];
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
                        
                        case "im_created": HandleIMSessionCreated(jObject);
                            break;
                        case "channel_created": HandleChannelCreated(jObject);
                            break;

                        case "team_join": HandleTeamJoin(jObject);
                            break;
                        case "user_change": HandleUserChange(jObject);
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
            var message = JsonConvert.DeserializeObject<SlackMessage>(json);
            var slackUser = slackUsers.SingleOrDefault(u => u.Id == message.UserId);
            var channel = channelSessions.SingleOrDefault(c => c.Id == message.ChannelId);
            var instantMessage = imSessions.SingleOrDefault(im => im.Id == message.ChannelId);
            
            if (slackUser != null && slackUser.Id != selfId)
            {
                if (channel != null && message.Text.StartsWith("tipper"))
                {
                    await slackHandler.TipBotChannelCommand(message, slackUser, channel);
                }
                else if (instantMessage != null)
                {
                    await slackHandler.InstantMessageCommand(message.Text, slackUser, instantMessage);
                }
            }
        }

        private void HandleIMSessionCreated(JObject jObject)
        {
            var imSession = new SlackIMSession
            {
                Id = (string)jObject["channel"]["id"],
                UserId = (string)jObject["user"]
            };
            imSessions.Add(imSession);

            var slackUser = slackUsers.Single(u => u.Id == imSession.UserId);
            logger.LogTrace($"IM session with user {slackUser.Name} was created.");
        }

        private void HandleChannelCreated(JObject jObject)
        {
            var channelSession = JsonConvert.DeserializeObject<SlackChannelSession>(jObject["channel"].ToString());
            channelSessions.Add(channelSession);
            logger.LogTrace($"#{channelSession.Name} was created.");
        }

        private void HandleTeamJoin(JObject jObject)
        {
            var slackUser = JsonConvert.DeserializeObject<SlackUser>(jObject["user"].ToString());
            logger.LogTrace($"User {slackUser.Name} has joined the team (id: {slackUser.Id}).");
            slackUsers.Add(slackUser);
        }

        private void HandleUserChange(JObject jObject)
        {
            var slackUser = JsonConvert.DeserializeObject<SlackUser>(jObject["user"].ToString());
            var oldSlackUser = slackUsers.Single(u => u.Id == slackUser.Id);
            if (!string.Equals(oldSlackUser.Name, slackUser.Name))
            {
                logger.LogTrace($"User {oldSlackUser.Name} changed his username to {slackUser.Name} (id: {slackUser.Id})");
                oldSlackUser.Name = slackUser.Name;
            }
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
            using (var response = await httpClient.GetAsync($"https://slack.com/api/chat.postMessage?token={apiToken}&channel={channelId}&text={message}&unfurl_links={unfurl_links}&as_user=true"))
            using (var content = response.Content)
            {
                var json = await content.ReadAsStringAsync();
                logger.LogTrace($"Reply from request to chat.postMessage: {json}");
            }
        }

        public async Task<string> GetInstantMessageId(string userId)
        {
            var id = imSessions.SingleOrDefault(im => im.UserId == userId)?.Id;
            if (id == null)
            {
                logger.LogTrace($"Requesting im.open with user {userId}");
                using (var httpClient = new HttpClient())
                using (var response = await httpClient.GetAsync($"https://slack.com/api/im.open?token={apiToken}&user={userId}&as_user=true"))
                using (var content = response.Content)
                {
                    var json = await content.ReadAsStringAsync();
                    logger.LogTrace($"Reply from request to im.open: {json}");
                    var jObject = JObject.Parse(json);
                    id = (string)jObject["channel"]["id"];
                    imSessions.Add(new SlackIMSession {Id = id, UserId = userId});
                }
            }
            return id;
        }
    }
}