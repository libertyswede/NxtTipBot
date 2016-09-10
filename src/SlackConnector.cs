using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NxtLib;

namespace NxtTipBot
{
    public class SlackConnector
    {
        private readonly string apiToken;
        private readonly ILogger logger;
        private readonly NxtConnector nxtConnector;

        private string selfId;
        private List<Channel> channels;
        private List<User> users;
        private List<InstantMessage> instantMessages;
        private ClientWebSocket webSocket;
        private readonly UTF8Encoding encoder = new UTF8Encoding();
        private int id = 1;

        public SlackConnector(string apiToken, ILogger logger, NxtConnector nxtConnector)
        {
            this.logger = logger;
            this.apiToken = apiToken;
            this.nxtConnector = nxtConnector;
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
                        case "presence_change":// ignore these
                        case "reconnect_url":
                        case "user_typing":
                            break;
                        
                        case "hello": logger.LogDebug("Hello recieved.");
                            break;
                        case "message": await HandleMessage(json);
                            break;
                        
                        case "im_created": HandleIMCreated(jObject);
                            break;
                        case "channel_created": HandleChannelCreated(jObject);
                            break;
                        
                        case null: HandleNullType(jObject, json);
                            break;
                        default: logger.LogDebug(json);
                            break; 
                    }
                }
            }
        }

        private async Task HandleMessage(string json)
        {
            logger.LogTrace(json);
            var message = JsonConvert.DeserializeObject<Message>(json);
            var user = users.SingleOrDefault(u => u.Id == message.User);
            var channel = channels.SingleOrDefault(c => c.Id == message.Channel);
            var instantMessage = instantMessages.SingleOrDefault(im => im.Id == message.Channel);
            
            if (user != null && user.Id != selfId)
            {
                if (channel != null && message.Text.StartsWith("tipbot"))
                {
                    await HandleTipBotCommand(message, user, channel);
                }
                else if (instantMessage != null)
                {
                    await HandleIMMessage(message, user, instantMessage);
                }
            }
        }

        private Task HandleTipBotCommand(Message message, User user, Channel channel)
        {
            logger.LogDebug($"Tip command recieved from {user.Name} in {channel.Name}: {message.Text}");
            return Task.CompletedTask;
        }

        private async Task HandleIMMessage(Message message, User user, InstantMessage instantMessage)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                await SendMessage(instantMessage.Id, "huh? try typing *help* for a list of available commands.");
            }
            else if (message.Text.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                const string helpText = 
                @"*Direct Message Commands*
_balance_ - Wallet balance
_deposit_ - shows your deposit address (or creates one if you don't have one already)
_withdraw [nxt address] amount_ - withdraws amount (in NXT) to specified NXT address

*Channel Commands*
_tipbot tip [user or nxt address] amount_ - sends a tip to specified user or address";
                await SendMessage(instantMessage.Id, helpText);
            }
            else if (message.Text.Equals("balance", StringComparison.OrdinalIgnoreCase))
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    await SendMessage(instantMessage.Id, $"You do currently not have an account, try *deposit* command to create one.");
                    return;
                }
                var balance = await nxtConnector.GetBalance(account);
                await SendMessage(instantMessage.Id, $"Your current balance is {balance} NXT.");
            }
            else if (message.Text.Equals("deposit", StringComparison.OrdinalIgnoreCase))
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    await SendMessage(instantMessage.Id, $"I have created account with address: {account.NxtAccountRs} for you.");
                }
                else
                {
                    await SendMessage(instantMessage.Id, $"You can deposit NXT here: {account.NxtAccountRs}");
                }
            }
            else if (message.Text.StartsWith("withdraw", StringComparison.OrdinalIgnoreCase))
            {
                var regex = new Regex("^withdraw (NXT-[A-Z0-9\\-]+) ([0-9\\.]+)");
                var match = regex.Match(message.Text);
                if (match.Success)
                {
                    var account = await nxtConnector.GetAccount(user.Id);
                    if (account == null)
                    {
                        await SendMessage(instantMessage.Id, $"You do not have an account.");
                        return;
                    }

                    var address = match.Groups[1].Value;
                    var amount = Amount.CreateAmountFromNxt(decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
                    
                    var balance = await nxtConnector.GetBalance(account);
                    if (balance < amount.Nxt + Amount.OneNxt.Nxt)
                    {
                        await SendMessage(instantMessage.Id, $"Not enough funds.");
                        return;
                    }

                    var txId = await nxtConnector.SendMoney(account, address, amount, "withdraw requested");
                    await SendMessage(instantMessage.Id, $"{amount.Nxt} NXT was sent to the specified address, (https://nxtportal.org/transactions/{txId})");
                }
                else
                {
                    await SendMessage(instantMessage.Id, "huh? try typing *help* for a list of available commands.");
                }
            }
            else
            {
                await SendMessage(instantMessage.Id, "huh? try typing *help* for a list of available commands.");
            }
        }

        private void HandleIMCreated(JObject jObject)
        {
            var instantMessage = new InstantMessage
            {
                Id = (string)jObject["channel"]["id"],
                User = (string)jObject["user"]
            };
            instantMessages.Add(instantMessage);

            var user = users.Single(u => u.Id == instantMessage.User);
            logger.LogDebug($"IM with user {user.Name} was created.");
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
                logger.LogDebug(json);
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