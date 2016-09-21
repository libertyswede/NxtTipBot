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
        Task InstantMessageRecieved(string message, User user, InstantMessage instantMessage);
        Task TipBotChannelCommand(Message message, User user, Channel channel);
        void AddCurrency(Currency currency);
    }

    public class SlackHandler : ISlackHandler
    {

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

        public void AddCurrency(Currency currency)
        {
            currencies.Add(currency);
        }

        public async Task InstantMessageRecieved(string message, User user, InstantMessage instantMessage)
        {
            var messageText = message.Trim();
            Match match = null;

            if (string.IsNullOrEmpty(messageText))
            {
                await UnknownCommand(instantMessage);
            }
            else if (IsSingleWordCommand(messageText, "help"))
            {
                await Help(instantMessage);
            }
            else if (IsSingleWordCommand(messageText, "balance"))
            {
                await Balance(user, instantMessage);
            }
            else if (IsSingleWordCommand(messageText, "deposit"))
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

        public async Task TipBotChannelCommand(Message message, User user, Channel channel)
        {
            var messageText = message?.Text.Trim();
            Match match = null;

            if ((match = IsTipCommand(messageText)).Success)
            {
                await Tip(user, match, channel);
            }
            else
            {
                await SlackConnector.SendMessage(channel.Id, MessageConstants.UnknownChannelCommandReply);
            }
        }

        private async Task UnknownCommand(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.UnknownCommandReply);
        }

        private async Task Help(InstantMessage instantMessage)
        {
            await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.HelpText);
        }

        private async Task Balance(User user, InstantMessage instantMessage)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                // This could be improved with a fancy "do you want to create new account" - button which exists in the Slack API.
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.NoAccount);
                return;
            }
            var balance = await nxtConnector.GetBalance(account);
            await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.CurrentBalance(balance));
            foreach (var currency in currencies)
            {
                var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
                if (currencyBalance > 0)
                {
                    await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.CurrentCurrencyBalance(currencyBalance, currency.Code));
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
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.AccountCreated(account.NxtAccountRs));
            }
            else
            {
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.DepositAddress(account.NxtAccountRs));
            }
        }

        private async Task Withdraw(User user, InstantMessage instantMessage, Match match)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.NoAccount);
                return;
            }

            var address = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? "NXT" : match.Groups[3].Value;

            if (!string.Equals(unit, "NXT", StringComparison.OrdinalIgnoreCase))
            {
                var currency = currencies.SingleOrDefault(c => c.Code == unit);
                if (currency != null)
                {
                    await WithdrawCurrency(instantMessage, currency, account, address, amountToWithdraw);
                }
                else
                {
                    await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.UnknownUnit(unit));
                }
                return;
            }
            var amount = Amount.CreateAmountFromNxt(amountToWithdraw);

            var balance = await nxtConnector.GetBalance(account);
            if (balance < amount.Nxt + Amount.OneNxt.Nxt)
            {
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.NotEnoughFunds(balance, unit));
                return;
            }

            try
            {
                var txId = await nxtConnector.SendMoney(account, address, amount, "withdraw from slack tipbot");
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.Withdraw(amount.Nxt, unit, txId));
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.InvalidAddress);
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

        private async Task WithdrawCurrency(InstantMessage instantMessage, Currency currency, NxtAccount account, string recipientAddressRs, decimal amountToWithdraw)
        {
            var nxtBalance = await nxtConnector.GetBalance(account);
            var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
            if (nxtBalance < 1)
            {
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.NotEnoughFunds(nxtBalance, "NXT"));
                return;
            }
            if (currencyBalance < amountToWithdraw)
            {
                await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.NotEnoughFunds(currencyBalance, currency.Code));
                return;
            }
            try
            {
                var unitsToWithdraw = (long)(amountToWithdraw * (long)Math.Pow(currency.Decimals, 10));
                var txId = await nxtConnector.TransferCurrency(account, recipientAddressRs, currency.CurrencyId, unitsToWithdraw, "withdraw from slack tipbot");
                var reply = MessageConstants.Withdraw(amountToWithdraw, currency.Code, txId);
                await SlackConnector.SendMessage(instantMessage.Id, reply);
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(instantMessage.Id, MessageConstants.InvalidAddress);
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

        private static bool IsSingleWordCommand(string message, string command)
        {
            return string.Equals(message, command, StringComparison.OrdinalIgnoreCase);
        }

        private static Match IsWithdrawCommand(string message)
        {
            var regex = new Regex("^\\s*(?i)withdraw(?-i) (NXT-[A-Z0-9\\-]+) ([0-9]+\\.?[0-9]*) ?([A-Za-z]+)?");
            var match = regex.Match(message);
            return match;
        }

        private async Task Tip(User user, Match match, Channel channel)
        {
            var account = await walletRepository.GetAccount(user.Id);
            if (account == null)
            {
                await SlackConnector.SendMessage(channel.Id, MessageConstants.NoAccountChannel);
                return;
            }

            var recipientUserId = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? "NXT" : match.Groups[3].Value;

            if (!string.Equals(unit, "NXT", StringComparison.OrdinalIgnoreCase))
            {
                var currency = currencies.SingleOrDefault(c => c.Code == unit);
                if (currency != null)
                {
                    await TipCurrency(channel, user, currency, account, recipientUserId, amountToWithdraw);
                }
                else
                {
                    await SlackConnector.SendMessage(channel.Id, MessageConstants.UnknownUnit(unit));
                }
                return;
            }

            var amount = Amount.CreateAmountFromNxt(amountToWithdraw);

            var balance = await nxtConnector.GetBalance(account);
            if (balance < amount.Nxt + Amount.OneNxt.Nxt)
            {
                await SlackConnector.SendMessage(channel.Id, MessageConstants.NotEnoughFunds(balance, unit));
                return;
            }
            var recipientAccount = await walletRepository.GetAccount(recipientUserId);
            if (recipientAccount == null)
            {
                recipientAccount = nxtConnector.CreateAccount(recipientUserId);
                await walletRepository.AddAccount(recipientAccount);
                var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
                await SlackConnector.SendMessage(imId, MessageConstants.TipRecieved(user.Id));
            }

            try
            {
                var txId = await nxtConnector.SendMoney(account, recipientAccount.NxtAccountRs, amount, "slackbot tip");
                var reply = MessageConstants.TipSentChannel(user.Id, recipientUserId, amount.Nxt, unit, txId);
                await SlackConnector.SendMessage(channel.Id, reply, false);
            }
            catch (NxtException e)
            {
                logger.LogError(0, e, e.Message);
                throw;
            }
        }

        private async Task TipCurrency(Channel channel, User user, Currency currency, NxtAccount account, string recipientUserId, decimal amountToWithdraw)
        {
            var nxtBalance = await nxtConnector.GetBalance(account);
            var currencyBalance = await nxtConnector.GetCurrencyBalance(currency.CurrencyId, account.NxtAccountRs);
            if (nxtBalance < 1)
            {
                await SlackConnector.SendMessage(channel.Id, MessageConstants.NotEnoughFunds(nxtBalance, "NXT"));
                return;
            }
            if (currencyBalance < amountToWithdraw)
            {
                await SlackConnector.SendMessage(channel.Id, MessageConstants.NotEnoughFunds(currencyBalance, currency.Code));
                return;
            }
            var recipientAccount = await walletRepository.GetAccount(recipientUserId);
            if (recipientAccount == null)
            {
                recipientAccount = nxtConnector.CreateAccount(recipientUserId);
                await walletRepository.AddAccount(recipientAccount);
                var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
                await SlackConnector.SendMessage(imId, MessageConstants.TipRecieved(user.Id));
            }
            try
            {
                var unitsToWithdraw = (long)(amountToWithdraw * (long)Math.Pow(currency.Decimals, 10));
                var txId = await nxtConnector.TransferCurrency(account, recipientAccount.NxtAccountRs, currency.CurrencyId, unitsToWithdraw, "slackbot tip");
                var reply = MessageConstants.TipSentChannel(user.Id, recipientUserId, amountToWithdraw, currency.Code, txId);
                await SlackConnector.SendMessage(channel.Id, reply);
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(channel.Id, MessageConstants.InvalidAddress);
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
            var regex = new Regex("^\\s*(?i)tipbot tip(?-i) <@([A-Za-z0-9]+)> ([0-9]+\\.?[0-9]*) ?([A-Za-z]+)?");
            var match = regex.Match(message);
            return match;
        }
    }
}