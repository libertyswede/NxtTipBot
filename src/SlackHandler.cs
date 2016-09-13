using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NxtLib;

namespace NxtTipbot
{
    public interface ISlackHandler
    {
        Task InstantMessageRecieved(Message message, User user, InstantMessage instantMessage);
        Task HandleTipBotChannelCommand(Message message, User user, Channel channel);
    }

    public class SlackHandler : ISlackHandler
    {
        const string HelpText = "*Direct Message Commands*\n"
            + "_balance_ - Wallet balance\n"
            + "_deposit_ - shows your deposit address (or creates one if you don't have one already)\n"
            + "_withdraw [nxt address] amount_ - withdraws amount (in NXT) to specified NXT address\n\n"
            + "*Channel Commands*\n"
            + "_tipbot tip @user amount_ - sends a tip to specified user or address";
            
        const string UnknownCommandReply = "huh? try typing *help* for a list of available commands.";

        private readonly INxtConnector nxtConnector;
        private readonly ILogger logger;
        public ISlackConnector SlackConnector { get; set; }

        public SlackHandler(INxtConnector nxtConnector, ILogger logger)
        {
            this.nxtConnector = nxtConnector;
            this.logger = logger;
        }

        public async Task InstantMessageRecieved(Message message, User user, InstantMessage instantMessage)
        {
            var messageText = message?.Text.Trim();
            Match match = null;

            if (string.IsNullOrEmpty(messageText))
            {
                await HandleUnknownCommand(instantMessage);
            }
            else if (IsSingleWordCommand("help", messageText))
            {
                await HandleHelpCommand(instantMessage);
            }
            else if (IsSingleWordCommand("balance", messageText))
            {
                await HandleBalanceCommand(user, instantMessage);
            }
            else if (IsSingleWordCommand("deposit", messageText))
            {
                await HandleDepositCommand(user, instantMessage);
            }
            else if ((match = IsWithdrawCommand(messageText)).Success)
            {
                await HandleWithdrawCommand(user, instantMessage, match);
            }
            else
            {
                await HandleUnknownCommand(instantMessage);
            }
        }

        private async Task HandleUnknownCommand(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, UnknownCommandReply);
        }

        private async Task HandleHelpCommand(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, HelpText);
        }

        private async Task HandleBalanceCommand(User user, InstantMessage instantMessage)
        {
            var account = await nxtConnector.GetAccount(user.Id);
            if (account == null)
            {
                // This could be improved with a fancy "do you want to create new account" - button which exists in the Slack API.
                await SlackConnector.SendMessage(instantMessage.Id, "You do currently not have an account, try *deposit* command to create one.");
            }
            else
            {
                var balance = await nxtConnector.GetBalance(account);
                await SlackConnector.SendMessage(instantMessage.Id, $"Your current balance is {balance} NXT.");
            }
        }

        private async Task HandleDepositCommand(User user, InstantMessage instantMessage)
        {
            var account = await nxtConnector.GetAccount(user.Id);
            if (account == null)
            {
                account = await nxtConnector.CreateAccount(user.Id);
                var reply = $"I have created account with address: {account.NxtAccountRs} for you.\n"
                            + "Please do not deposit large amounts of NXT, as it is not a secure wallet like the core client or mynxt wallets.";
                await SlackConnector.SendMessage(instantMessage.Id, reply);
            }
            else
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"You can deposit NXT here: {account.NxtAccountRs}");
            }
        }

        private async Task HandleWithdrawCommand(User user, InstantMessage instantMessage, Match match)
        {
            var account = await nxtConnector.GetAccount(user.Id);
            if (account == null)
            {
                await SlackConnector.SendMessage(instantMessage.Id, "You do not have an account.");
                return;
            }

            var address = match.Groups[1].Value;
            var amount = Amount.CreateAmountFromNxt(decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));

            var balance = await nxtConnector.GetBalance(account);
            if (balance < amount.Nxt + Amount.OneNxt.Nxt)
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"Not enough funds. You only have {balance} NXT.");
                return;
            }

            try
            {
                var txId = await nxtConnector.SendMoney(account, address, amount, "withdraw from slack tipbot requested");
                var reply = $"{amount.Nxt} NXT was sent to the specified address, (https://nxtportal.org/transactions/{txId})";
                await SlackConnector.SendMessage(instantMessage.Id, reply);
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(instantMessage.Id, "Not a valid NXT address");
                }
                else
                {
                    logger.LogError(0, e, e.Message);
                    throw;
                }
            }
            catch (NxtException e)
            {
                logger.LogError(0, e, e.Message);
                throw;
            }
        }

        private static bool IsSingleWordCommand(string command, string message)
        {
            return message.Equals(command, StringComparison.OrdinalIgnoreCase);
        }

        private static Match IsWithdrawCommand(string message)
        {
            var regex = new Regex("^withdraw (NXT-[A-Z0-9\\-]+) ([0-9\\.]+)");
            var match = regex.Match(message);
            return match;
        }

        public async Task HandleTipBotChannelCommand(Message message, User user, Channel channel)
        {
            var messageText = message?.Text.Trim();
            Match match = null;

            if ((match = IsTipCommand(messageText)).Success)
            {
                await HandleTipCommand(user, match, channel);
            }
            else
            {
                await SlackConnector.SendMessage(channel.Id, "huh? try sending me *help* in a direct message for a list of available commands.");
            }
        }

        private async Task HandleTipCommand(User user, Match match, Channel channel)
        {
            var account = await nxtConnector.GetAccount(user.Id);
            if (account == null)
            {
                const string reply =  "Sorry mate, you do not have an account. Try sending me *help* in a direct message and I'll help you out set one up.";
                await SlackConnector.SendMessage(channel.Id, reply);
                return;
            }

            var recipientUser = match.Groups[1].Value;
            var amount = Amount.CreateAmountFromNxt(decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));

            var balance = await nxtConnector.GetBalance(account);
            if (balance < amount.Nxt + Amount.OneNxt.Nxt)
            {
                await SlackConnector.SendMessage(channel.Id, "Not enough funds.");
                return;
            }
            var recipientAccount = await nxtConnector.GetAccount(recipientUser);
            if (recipientAccount == null)
            {
                recipientAccount = await nxtConnector.CreateAccount(recipientUser);
                var imId = await SlackConnector.GetInstantMessageId(recipientUser);
                var reply = $"Hi, you recieved a tip from <@{user.Id}>.\n" +
                            "So I have set up an account for you that you can use." +
                            "Type *help* to get more information about what commands are available.";
                await SlackConnector.SendMessage(imId, reply);
            }

            try
            {
                var txId = await nxtConnector.SendMoney(account, recipientAccount.NxtAccountRs, amount, "slackbot tip");
                var reply = $"<@{user.Id}> => <@{recipientUser}> {amount.Nxt} NXT (https://nxtportal.org/transactions/{txId})";
                await SlackConnector.SendMessage(channel.Id, reply, false);
            }
            catch (NxtException e)
            {
                logger.LogError(0, e, e.Message);
                throw;
            }
        }

        private static Match IsTipCommand(string message)
        {
            var regex = new Regex("^tipbot tip <@([A-Za-z0-9]+)> ([0-9\\.]+)");
            var match = regex.Match(message);
            return match;
        }
    }
}