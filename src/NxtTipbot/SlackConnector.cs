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
        string SelfId { get; }
        string SelfName { get; }
        Task SendMessage(string channelId, string message, bool unfurl_links = true);
        Task<string> GetInstantMessageId(string userId);
        SlackUser GetUser(string userId);
    }

    public class SlackConnector : ISlackConnector
    {
        private readonly string apiToken;
        private readonly ILogger logger;
        private readonly ISlackHandler slackHandler;

        public string SelfId { get; private set; }
        public string SelfName { get; private set; }

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
            var lastConnectTry = DateTime.MinValue;
            while (DateTime.Compare(lastConnectTry.AddMinutes(1), DateTime.UtcNow) < 0)
            {
                try
                {
                    string websocketUri = string.Empty;
                    List<SlackChannelSession> groupSessions;
                    List<SlackChannelSession> mpimSessions;
                    using (var httpClient = new HttpClient())
                    using (var response = await httpClient.GetAsync($"https://slack.com/api/rtm.start?token={apiToken}&simple_latest=true&no_unreads=true&mpim_aware=true"))
                    using (var content = response.Content)
                    {
                        var json = await content.ReadAsStringAsync();
                        logger.LogTrace($"Initial handshake reply with rtm.start: {json}");
                        var jObject = JObject.Parse(json);
                        websocketUri = (string)jObject["url"];
                        SelfId = (string)jObject["self"]["id"];
                        SelfName = (string)jObject["self"]["name"];
                        channelSessions = JsonConvert.DeserializeObject<List<SlackChannelSession>>(jObject["channels"].ToString());
                        groupSessions = JsonConvert.DeserializeObject<List<SlackChannelSession>>(jObject["groups"].ToString());
                        mpimSessions = JsonConvert.DeserializeObject<List<SlackChannelSession>>(jObject["mpims"].ToString());
                        slackUsers = JsonConvert.DeserializeObject<List<SlackUser>>(jObject["users"].ToString());
                        imSessions = JsonConvert.DeserializeObject<List<SlackIMSession>>(jObject["ims"].ToString());
                    }

                    var channels = string.Join(", ", channelSessions.Where(s => s.IsMember).Select(s => s.Name));
                    var groups = string.Join(", ", groupSessions.Where(s => s.IsMember).Select(s => s.Name));
                    var mpims = string.Join(", ", mpimSessions.Where(s => s.IsMember).Select(s => s.Name));
                    channelSessions.AddRange(groupSessions.Union(mpimSessions));
                    logger.LogTrace($"I'm currently in these channels: {channels}");
                    logger.LogTrace($"I'm currently in these private channels: {groups}");
                    logger.LogTrace($"I'm currently in these Multiparty IM's: {mpims}");

                    webSocket = new ClientWebSocket();
                    lastConnectTry = DateTime.UtcNow;
                    await webSocket.ConnectAsync(new Uri(websocketUri), CancellationToken.None);
                    await Recieve();
                }
                catch (Exception e)
                {
                    logger.LogCritical($"Unhandled exception: {e.ToString()}\n{e.Message}\n{e.StackTrace}\nAttempting to reconnect..");
                }
            }
        }

        private async Task Recieve()
        {
            WebSocketReceiveResult result;
            var buffer = new byte[8192];

            while (webSocket.State == WebSocketState.Open)
            {
                string json = string.Empty;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        logger.LogInformation("MessageType.Close recieved, closing connection to Slack.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        return;
                    }

                    json += encoder.GetString(buffer, 0, result.Count);
                } while (result.Count == buffer.Length || !json.TrimEnd().EndsWith("}"));

                JObject jObject = null;
                try
                {
                    jObject = JObject.Parse(json);
                }
                catch (Exception)
                {
                    logger.LogCritical($"Error when parsing json, length: {json.Length}, json text: {json}");
                    throw;
                }
                var type = (string)jObject["type"];
                switch (type)
                {
                    case "presence_change":// ignore these
                    case "reconnect_url":
                    case "user_typing":
                    case "hello":
                    case "reaction_removed":
                    case "file_shared":
                    case "file_public":
                    case "dnd_updated_user":
                    case "pin_added":
                    case "pin_removed":
                    case "desktop_notification":
                        break;

                    case "reaction_added": await HandleReactionAdded(json);
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

        private async Task HandleReactionAdded(string json)
        {
            var reaction = JsonConvert.DeserializeObject<SlackReaction>(json);
            var slackUser = slackUsers.SingleOrDefault(u => u.Id == reaction.UserId);
            var recipientSlackUser = slackUsers.SingleOrDefault(u => u.Id == reaction.ItemUserId);
            var channel = channelSessions.SingleOrDefault(c => c.Id == reaction.Item.ChannelId);

            if (slackUser != null && recipientSlackUser != null && slackUser.Id != SelfId && channel != null)
            {
                await slackHandler.TipBotReactionCommand(reaction, slackUser, recipientSlackUser, channel);
            }
        }

        private async Task HandleMessage(string json)
        {
            var message = JsonConvert.DeserializeObject<SlackMessage>(json);
            var slackUser = slackUsers.SingleOrDefault(u => u.Id == message.UserId);
            var channel = channelSessions.SingleOrDefault(c => c.Id == message.ChannelId);
            var instantMessage = imSessions.SingleOrDefault(im => im.Id == message.ChannelId);
            
            if (slackUser != null && slackUser.Id != SelfId)
            {
                if (channel != null && message.Text.StartsWith(SelfName, StringComparison.OrdinalIgnoreCase) || message.Text.StartsWith($"<@{SelfId}>"))
                {
                    await slackHandler.TipBotChannelCommand(message, slackUser, channel);
                }
                else if (instantMessage != null)
                {
                    logger.LogTrace($"Recieved IM from: {slackUser.Name} ({slackUser.Id}), message: {message.Text}");
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
            if (imSessions.All(im => im.Id != imSession.Id))
            {
                imSessions.Add(imSession);
                var slackUser = slackUsers.Single(u => u.Id == imSession.UserId);
                logger.LogTrace($"IM session with user {slackUser.Name} was created.");
            }
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
            var debugTarget = channelSessions.SingleOrDefault(s => s.Id == channelId)?.Name ?? 
                slackUsers.Single(u => u.Id == imSessions.Single(s => s.Id == channelId).UserId).Name;
            message = message.Replace("&", "%26amp;");
            logger.LogTrace($"Sending chat.postMessage to: {debugTarget} ({channelId}), message: {message}");
            using (var httpClient = new HttpClient())
            using (var response = await httpClient.GetAsync($"https://slack.com/api/chat.postMessage?token={apiToken}&channel={channelId}&text={message}&unfurl_links={unfurl_links}&as_user=true"))
            using (var content = response.Content)
            {
                var json = await content.ReadAsStringAsync();
                //logger.LogTrace($"Reply from request to chat.postMessage: {json}");
            }
        }

        public async Task<string> GetInstantMessageId(string userId)
        {
            if (userId == SelfId)
            {
                throw new ArgumentException("Cannot instant message with yourself", nameof(userId));
            }
            var id = imSessions.SingleOrDefault(im => im.UserId == userId)?.Id;
            if (id == null)
            {
                var userName = slackUsers.Single(u => u.Id == userId).Name;
                logger.LogTrace($"Requesting im.open with user {userName} ({userId})");
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

        public SlackUser GetUser(string userId)
        {
            return slackUsers.SingleOrDefault(u => u.Id == userId);
        }
    }
}