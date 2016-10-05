using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NxtTipbot.Model;
using Microsoft.Extensions.Logging;
using NxtLib;

namespace NxtTipbot
{
    public interface ISlackHandler
    {
        Task InstantMessageCommand(string message, SlackUser slackUser, SlackIMSession imSession);
        Task TipBotChannelCommand(SlackMessage message, SlackUser slackUser, SlackChannelSession channelSession);
        void AddTransferable(NxtTransferable transferable);
    }

    public class SlackHandler : ISlackHandler
    {

        private readonly INxtConnector nxtConnector;
        private readonly IWalletRepository walletRepository;
        private readonly ILogger logger;
        private readonly List<NxtTransferable> transferables = new List<NxtTransferable> { Nxt.Singleton };
        public ISlackConnector SlackConnector { get; set; }

        public SlackHandler(INxtConnector nxtConnector, IWalletRepository walletRepository, ILogger logger)
        {
            this.nxtConnector = nxtConnector;
            this.walletRepository = walletRepository;
            this.logger = logger;
        }

        public void AddTransferable(NxtTransferable transferable)
        {
            if (transferables.Any(t => t.Name.Equals(transferable.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException(nameof(transferable), $"Name of transferable must be unique, {transferable.Name} was already added.");
            }
            transferables.Add(transferable);
        }

        public async Task InstantMessageCommand(string message, SlackUser slackUser, SlackIMSession imSession)
        {
            var messageText = message.Trim();
            Match match = null;

            if (string.IsNullOrEmpty(messageText))
            {
                await UnknownCommand(imSession);
            }
            else if (IsSingleWordCommand(messageText, "help"))
            {
                await Help(imSession);
            }
            else if (IsSingleWordCommand(messageText, "balance"))
            {
                await Balance(slackUser, imSession);
            }
            else if (IsSingleWordCommand(messageText, "deposit"))
            {
                await Deposit(slackUser, imSession);
            }
            else if ((match = IsWithdrawCommand(messageText)).Success)
            {
                await Withdraw(slackUser, imSession, match);
            }
            else
            {
                await UnknownCommand(imSession);
            }
        }

        public async Task TipBotChannelCommand(SlackMessage message, SlackUser slackUser, SlackChannelSession channelSession)
        {
            var messageText = message?.Text.Trim();
            Match match = null;

            if ((match = IsTipCommand(messageText)).Success)
            {
                await Tip(slackUser, match, channelSession);
            }
            else
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.UnknownChannelCommandReply);
            }
        }

        private async Task UnknownCommand(SlackIMSession imSession)
        {
            await SlackConnector.SendMessage(imSession.Id, MessageConstants.UnknownCommandReply);
        }

        private async Task Help(SlackIMSession imSession)
        {
            await SlackConnector.SendMessage(imSession.Id, MessageConstants.HelpText);
        }

        private async Task Balance(SlackUser slackUser, SlackIMSession imSession)
        {
            var account = await walletRepository.GetAccount(slackUser.Id);
            if (account == null)
            {
                // This could be improved with a fancy "do you want to create new account" - button which exists in the Slack API.
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.NoAccount);
                return;
            }
            string message = "";
            foreach (var transferable in transferables)
            {
                var balance = await nxtConnector.GetBalance(transferable, account.NxtAccountRs);
                if (balance > 0)
                {
                    message += MessageConstants.CurrentBalance(balance, transferable) + "\n";
                }
            }
            await SlackConnector.SendMessage(imSession.Id, message.TrimEnd());
        }

        private async Task Deposit(SlackUser slackUser, SlackIMSession imSession)
        {
            var account = await walletRepository.GetAccount(slackUser.Id);
            if (account == null)
            {
                account = await CreateNxtAccount(slackUser.Id);
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.AccountCreated(account.NxtAccountRs));
            }
            else
            {
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.DepositAddress(account.NxtAccountRs));
            }
        }

        private async Task<NxtAccount> CreateNxtAccount(string slackUserId)
        {
            NxtAccount account = new NxtAccount { SlackId = slackUserId };
            await walletRepository.AddAccount(account);
            nxtConnector.SetNxtProperties(account);
            await walletRepository.UpdateAccount(account);
            return account;
        }

        private async Task<bool> VerifyParameters(NxtTransferable transferable, string unit, NxtAccount account, string slackSessionId, decimal amount)
        {
            if (transferable == null)
            {
                await SlackConnector.SendMessage(slackSessionId, MessageConstants.UnknownUnit(unit));
                return false;
            }

            var nxtBalance = await nxtConnector.GetBalance(Nxt.Singleton, account.NxtAccountRs);
            if (transferable == Nxt.Singleton)
            {
                if (amount > nxtBalance - 1)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFunds(nxtBalance, transferable.Name));
                    return false;
                }
            }
            else
            {
                if (nxtBalance < 1)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFunds(nxtBalance, Nxt.Singleton.Name));
                    return false;
                }

                var balance = await nxtConnector.GetBalance(transferable, account.NxtAccountRs);
                if (balance < amount)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFunds(balance, transferable.Name));
                    return false;
                }
            }
            return true;
        }

        private async Task Withdraw(SlackUser slackUser, SlackIMSession imSession, Match match)
        {
            var address = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? Nxt.Singleton.Name : match.Groups[3].Value;
            var transferable = transferables.SingleOrDefault(t => t.Name.Equals(unit, StringComparison.OrdinalIgnoreCase));
            var account = await walletRepository.GetAccount(slackUser.Id);

            if (account == null)
            {
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.NoAccount);
                return;
            }
            if (!(await VerifyParameters(transferable, unit, account, imSession.Id, amountToWithdraw)))
            {
                return;
            }
            try
            {
                var txId = await nxtConnector.Transfer(account, address, transferable, amountToWithdraw, "withdraw from slack tipper");
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.Withdraw(amountToWithdraw, transferable.Name, txId), false);
            }
            catch (ArgumentException e)
            {
                if (e.Message.Contains("not a valid reed solomon address"))
                {
                    await SlackConnector.SendMessage(imSession.Id, MessageConstants.InvalidAddress);
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

        private async Task Tip(SlackUser slackUser, Match match, SlackChannelSession channelSession)
        {
            var recipientUserId = match.Groups[1].Value;
            var amountToWithdraw = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups[3].Value) ? Nxt.Singleton.Name : match.Groups[3].Value;
            var transferable = transferables.SingleOrDefault(t => t.Name.Equals(unit, StringComparison.OrdinalIgnoreCase));
            var account = await walletRepository.GetAccount(slackUser.Id);

            if (recipientUserId == SlackConnector.SelfId)
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.CantTipBotChannel);
                return;
            }
            if (recipientUserId == slackUser.Id)
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.CantTipYourselfChannel);
                return;
            }
            if (account == null)
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.NoAccountChannel);
                return;
            }
            if (!(await VerifyParameters(transferable, unit, account, channelSession.Id, amountToWithdraw)))
            {
                return;
            }

            var recipientAccount = await walletRepository.GetAccount(recipientUserId);
            var recipientPublicKey = "";
            if (recipientAccount == null)
            {
                recipientAccount = await SendTipRecievedInstantMessage(slackUser, recipientUserId);
                recipientPublicKey = recipientAccount.NxtPublicKey;
            }

            try
            {
                var txId = await nxtConnector.Transfer(account, recipientAccount.NxtAccountRs, transferable, amountToWithdraw, "tip from slack tipper", recipientPublicKey);
                var reply = MessageConstants.TipSentChannel(slackUser.Id, recipientUserId, amountToWithdraw, transferable.Name, txId);
                await SlackConnector.SendMessage(channelSession.Id, reply, false);
            }
            catch (NxtException e)
            {
                logger.LogError(0, e, e.Message);
                throw;
            }
        }

        private async Task<NxtAccount> SendTipRecievedInstantMessage(SlackUser slackUser, string recipientUserId)
        {
            var recipientAccount = await CreateNxtAccount(recipientUserId);
            var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
            await SlackConnector.SendMessage(imId, MessageConstants.TipRecieved(slackUser.Id));
            return recipientAccount;
        }

        private static Match IsTipCommand(string message)
        {
            var regex = new Regex("^\\s*(?i)tipper tip(?-i) <@([A-Za-z0-9]+)> ([0-9]+\\.?[0-9]*) ?([A-Za-z]+)?");
            var match = regex.Match(message);
            return match;
        }
    }
}