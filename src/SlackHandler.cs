using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NxtLib;
using NxtLib.MonetarySystem;

namespace NxtTipbot
{
    public interface ISlackHandler
    {
        Task InstantMessageRecieved(Message message, User user, InstantMessage instantMessage);
        Task HandleTipBotChannelCommand(Message message, User user, Channel channel);
        Task AddCurrency(ulong currencyId);
    }

    public class SlackHandler : ISlackHandler
    {
        const string HelpText = "*Direct Message Commands*\n"
            + "_balance_ - Wallet balance\n"
            + "_deposit_ - shows your deposit address (or creates one if you don't have one already)\n"
            + "_withdraw [nxt address] amount [unit]_ - withdraws amount to specified NXT address\n\n"
            + "*Channel Commands*\n"
            + "_tipbot tip @user amount [unit]_ - sends a tip to specified user";
            
        const string UnknownCommandReply = "huh? try typing *help* for a list of available commands.";

        private readonly INxtConnector nxtConnector;
        private readonly IWalletRepository walletRepository;
        private readonly ILogger logger;
        private readonly List<Currency> currencies = new List<Currency>();
        public ISlackConnector SlackConnector { get; set; }

        public SlackHandler(INxtConnector nxtConnector, IWalletRepository walletRepository, ILogger logger)
        {
            this.nxtConnector = nxtConnector;
            this.walletRepository = walletRepository;
            this.logger = logger;
        }

        public async Task AddCurrency(ulong currencyId)
        {
            var currency = await nxtConnector.GetCurrency(currencyId);
            currencies.Add(currency);
        }

        public async Task InstantMessageRecieved(Message message, User user, InstantMessage instantMessage)
        {
            var messageText = message?.Text.Trim();
            Match match = null;

            if (string.IsNullOrEmpty(messageText))
            {
                await UnknownCommand(instantMessage);
            }
            else if (IsSingleWordCommand("help", messageText))
            {
                await Help(instantMessage);
            }
            else if (IsSingleWordCommand("balance", messageText))
            {
                await Balance(user, instantMessage);
            }
            else if (IsSingleWordCommand("deposit", messageText))
            {
                await Deposit(user, instantMessage);
            }
            else if ((match = IsWithdrawCommand(messageText)).Success)
            {
                await Withdraw(user, instantMessage, match);
            }
            else
            {
                await UnknownCommand(instantMessage);
            }
        }

        private async Task UnknownCommand(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, UnknownCommandReply);
        }

        private async Task Help(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, HelpText);
        }

        private async Task Balance(User user, InstantMessage instantMessage)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                // This could be improved with a fancy "do you want to create new account" - button which exists in the Slack API.
                await SlackConnector.SendMessage(instantMessage.Id, "You do currently not have an account, try *deposit* command to create one.");
            }
            else
            {
                var balance = await nxtConnector.GetBalance(account);
                await SlackConnector.SendMessage(instantMessage.Id, $"Your current balance is {balance} NXT.");
                foreach (var currency in currencies)
                {
                    var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
                    if (currencyBalance > 0)
                    {
                        await SlackConnector.SendMessage(instantMessage.Id, $"You also have {currencyBalance} {currency.Code}.");
                    }
                }
            }
        }

        private async Task Deposit(User user, InstantMessage instantMessage)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                account = nxtConnector.CreateAccount(user.Id);
                await walletRepository.AddAccount(account);
                var reply = $"I have created account with address: {account.NxtAccountRs} for you.\n"
                            + "Please do not deposit large amounts, as it is not a secure wallet like the core client or mynxt wallets.";
                await SlackConnector.SendMessage(instantMessage.Id, reply);
            }
            else
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"You can deposit NXT here: {account.NxtAccountRs}");
            }
        }

        private async Task Withdraw(User user, InstantMessage instantMessage, Match match)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                await SlackConnector.SendMessage(instantMessage.Id, "You do not have an account.");
                return;
            }

            var address = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? "NXT" : match.Groups[3].Value;

            if (!string.Equals(unit, "NXT", StringComparison.OrdinalIgnoreCase))
            {
                await WithdrawCurrency(instantMessage, unit, account, address, amountToWithdraw);
                return;
            }
            var amount = Amount.CreateAmountFromNxt(amountToWithdraw);

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

        private async Task WithdrawCurrency(InstantMessage instantMessage, string unit, NxtAccount account, string recipientAddressRs, decimal amountToWithdraw)
        {
            var currency = currencies.SingleOrDefault(c => c.Code == unit);
            if (currency == null)
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"Unknown currency {unit}");
                return;
            }
            var nxtBalance = await nxtConnector.GetBalance(account);
            var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
            if (nxtBalance < 1)
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"Not enough NXT to send transaction. You only have {nxtBalance} NXT.");
                return;
            }
            if (currencyBalance < amountToWithdraw)
            {
                await SlackConnector.SendMessage(instantMessage.Id, $"Not enough {unit}, you only have {currencyBalance} {unit}");
                return;
            }
            try
            {
                var unitsToWithdraw = (long)(amountToWithdraw * currency.Decimals);
                var txId = await nxtConnector.TransferCurrency(account, recipientAddressRs, currency.CurrencyId, unitsToWithdraw, "withdraw from slack tipbot requested");
                var reply = $"{amountToWithdraw} {unit} was transferred to the specified address, (https://nxtportal.org/transactions/{txId})";
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
            var regex = new Regex("^withdraw (NXT-[A-Z0-9\\-]+) ([0-9\\.]+) ?([A-Za-z]+)?");
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
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                const string reply =  "Sorry mate, you do not have an account. Try sending me *help* in a direct message and I'll help you out set one up.";
                await SlackConnector.SendMessage(channel.Id, reply);
                return;
            }

            var recipientUserId = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? "NXT" : match.Groups[3].Value;

            if (!string.Equals(unit, "NXT", StringComparison.OrdinalIgnoreCase))
            {
                await TipCurrency(channel, user, unit, account, recipientUserId, amountToWithdraw);
                return;
            }

            var amount = Amount.CreateAmountFromNxt(amountToWithdraw);

            var balance = await nxtConnector.GetBalance(account);
            if (balance < amount.Nxt + Amount.OneNxt.Nxt)
            {
                await SlackConnector.SendMessage(channel.Id, "Not enough funds.");
                return;
            }
            var recipientAccount = await walletRepository.GetAccount(recipientUserId);
            if (recipientAccount == null)
            {
                recipientAccount = nxtConnector.CreateAccount(recipientUserId);
                await walletRepository.AddAccount(recipientAccount);
                var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
                var reply = $"Hi, you recieved a tip from <@{user.Id}>.\n" +
                            "So I have set up an account for you that you can use." +
                            "Type *help* to get more information about what commands are available.";
                await SlackConnector.SendMessage(imId, reply);
            }

            try
            {
                var txId = await nxtConnector.SendMoney(account, recipientAccount.NxtAccountRs, amount, "slackbot tip");
                var reply = $"<@{user.Id}> => <@{recipientUserId}> {amount.Nxt} NXT (https://nxtportal.org/transactions/{txId})";
                await SlackConnector.SendMessage(channel.Id, reply, false);
            }
            catch (NxtException e)
            {
                logger.LogError(0, e, e.Message);
                throw;
            }
        }

        private async Task TipCurrency(Channel channel, User user, string unit, NxtAccount account, string recipientUserId, decimal amountToWithdraw)
        {
            var currency = currencies.SingleOrDefault(c => c.Code == unit);
            if (currency == null)
            {
                await SlackConnector.SendMessage(channel.Id, $"Unknown currency {unit}");
                return;
            }
            var nxtBalance = await nxtConnector.GetBalance(account);
            var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
            if (nxtBalance < 1)
            {
                await SlackConnector.SendMessage(channel.Id, $"Not enough NXT to send transaction. You only have {nxtBalance} NXT.");
                return;
            }
            if (currencyBalance < amountToWithdraw)
            {
                await SlackConnector.SendMessage(channel.Id, $"Not enough {unit}, you only have {currencyBalance} {unit}");
                return;
            }
            var recipientAccount = await walletRepository.GetAccount(recipientUserId);
            if (recipientAccount == null)
            {
                recipientAccount = nxtConnector.CreateAccount(recipientUserId);
                await walletRepository.AddAccount(recipientAccount);
                var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
                var reply = $"Hi, you recieved a tip from <@{user.Id}>.\n" +
                            "So I have set up an account for you that you can use." +
                            "Type *help* to get more information about what commands are available.";
                await SlackConnector.SendMessage(imId, reply);
            }
            try
            {
                var unitsToWithdraw = (long)(amountToWithdraw * currency.Decimals);
                var txId = await nxtConnector.TransferCurrency(account, recipientAccount.NxtAccountRs, currency.CurrencyId, unitsToWithdraw, "withdraw from slack tipbot requested");
                var reply = $"<@{user.Id}> => <@{recipientUserId}> {amountToWithdraw} {unit} (https://nxtportal.org/transactions/{txId})";
                await SlackConnector.SendMessage(channel.Id, reply);
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(channel.Id, "Not a valid NXT address");
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

        private static Match IsTipCommand(string message)
        {
            var regex = new Regex("^tipbot tip <@([A-Za-z0-9]+)> ([0-9\\.]+)");
            var match = regex.Match(message);
            return match;
        }
    }
}