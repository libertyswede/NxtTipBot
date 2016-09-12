using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NxtLib;

namespace NxtTipbot
{
    public class SlackHandler
    {
        const string HelpText = "*Direct Message Commands*\n"
            + "_balance_ - Wallet balance\n"
            + "_deposit_ - shows your deposit address (or creates one if you don't have one already)\n"
            + "_withdraw [nxt address] amount_ - withdraws amount (in NXT) to specified NXT address\n\n"
            + "*Channel Commands*\n"
            + "_tipbot tip @user amount_ - sends a tip to specified user or address";
            
        const string UnknownCommandReply = "huh? try typing *help* for a list of available commands.";

        private readonly NxtConnector nxtConnector;
        private readonly ILogger logger;

        public SlackHandler(NxtConnector nxtConnector, ILogger logger)
        {
            this.nxtConnector = nxtConnector;
            this.logger = logger;
        }

        public async Task<string> InstantMessageRecieved(Message message, User user, InstantMessage instantMessage)
        {
            var messageText = message?.Text.Trim();            
            Match match = null;

            if (string.IsNullOrEmpty(messageText))
            {
                return UnknownCommandReply;
            }
            else if (IsCommand("help", messageText))
            {
                return HelpText;
            }
            else if (IsCommand("balance", messageText))
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    return "You do currently not have an account, try *deposit* command to create one.";
                }
                var balance = await nxtConnector.GetBalance(account);
                return $"Your current balance is {balance} NXT.";
            }
            else if (IsCommand("deposit", messageText))
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    account = await nxtConnector.CreateAccount(user.Id);
                    return $"I have created account with address: {account.NxtAccountRs} for you.\n"
                           + "Please do not deposit large amounts of NXT, as it is not a secure wallet like the core client or mynxt wallets.";
                }
                else
                {
                    return $"You can deposit NXT here: {account.NxtAccountRs}";
                }
            }
            else if ((match = IsWithdrawCommand(messageText)).Success)
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    return "You do not have an account.";
                }

                var address = match.Groups[1].Value;
                var amount = Amount.CreateAmountFromNxt(decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));

                var balance = await nxtConnector.GetBalance(account);
                if (balance < amount.Nxt + Amount.OneNxt.Nxt)
                {
                    return $"Not enough funds. You only have {balance} NXT.";
                }

                try
                {
                    var txId = await nxtConnector.SendMoney(account, address, amount, "withdraw from slack tipbot requested");
                    return $"{amount.Nxt} NXT was sent to the specified address, (https://nxtportal.org/transactions/{txId})";
                }
                catch (ArgumentException e)
                {
                    if (e.Message.Contains("not a valid reed solomon address"))
                    {
                        return "Not a valid NXT address";
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
            return UnknownCommandReply;
        }

        private static bool IsCommand(string command, string message)
        {
            return message.Equals(command, StringComparison.OrdinalIgnoreCase);
        }

        private static Match IsWithdrawCommand(string message)
        {
            var regex = new Regex("^withdraw (NXT-[A-Z0-9\\-]+) ([0-9\\.]+)");
            var match = regex.Match(message);
            return match;
        }

        public async Task<string> HandleTipBotChannelCommand(Message message, User user, Channel channel)
        {
            logger.LogTrace($"Tip command recieved from {user.Name} in {channel.Name}: {message.Text}");

            var regex = new Regex("^tipbot tip <@([A-Za-z0-9]+)> ([0-9\\.]+)");
            var match = regex.Match(message.Text);
            if (match.Success)
            {
                var account = await nxtConnector.GetAccount(user.Id);
                if (account == null)
                {
                    return "Sorry m8, you do not have an account. Try sending me *help* in a direct message and I'll help you out set one up.";
                }

                var recipientUser = match.Groups[1].Value;
                var amount = Amount.CreateAmountFromNxt(decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
                
                var balance = await nxtConnector.GetBalance(account);
                if (balance < amount.Nxt + Amount.OneNxt.Nxt)
                {
                    return "Not enough funds.";
                }
                var recipientAccount = await nxtConnector.GetAccount(recipientUser);
                if (recipientAccount == null)
                {
                    recipientAccount = await nxtConnector.CreateAccount(recipientUser);
                    // TODO: Send IM to recipient about his new account
                }

                try
                {
                    var txId = await nxtConnector.SendMoney(account, recipientAccount.NxtAccountRs, amount, "slackbot tip");
                    return $"<@{user.Id}> => <@{recipientUser}> {amount.Nxt} NXT (https://nxtportal.org/transactions/{txId})";
                }
                catch (NxtException e)
                {
                    logger.LogError(0, e, e.Message);
                    throw;
                }
            }
            else
            {
                return "huh? try sending me *help* in a direct message for a list of available commands.";
            }
        }
    }
}