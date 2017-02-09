using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NxtTipbot.Model;
using Microsoft.Extensions.Logging;
using NxtLib;
using System.Collections.Generic;
using System.Linq;

namespace NxtTipbot
{
    public interface ISlackHandler
    {
        Task InstantMessageCommand(string message, SlackUser slackUser, SlackIMSession imSession);
        Task TipBotChannelCommand(SlackMessage message, SlackUser slackUser, SlackChannelSession channelSession);
    }

    public class SlackHandler : ISlackHandler
    {
        private readonly INxtConnector nxtConnector;
        private readonly IWalletRepository walletRepository;
        private readonly ILogger logger;
        private readonly ITransferables transferables;
        public ISlackConnector SlackConnector { get; set; }

        public SlackHandler(INxtConnector nxtConnector, IWalletRepository walletRepository, ITransferables transferables, ILogger logger)
        {
            this.nxtConnector = nxtConnector;
            this.walletRepository = walletRepository;
            this.transferables = transferables;
            this.logger = logger;
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
            else if (IsSingleWordCommand(messageText, "list"))
            {
                await List(imSession);
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
            await SlackConnector.SendMessage(imSession.Id, MessageConstants.GetHelpText(SlackConnector.SelfName));
        }

        private async Task List(SlackIMSession imSession)
        {
            var message = MessageConstants.ListCommandHeader;
            foreach (var transferable in transferables.NxtTransferables)
            {
                message += MessageConstants.ListCommandForTransferable(transferable);
            }
            await SlackConnector.SendMessage(imSession.Id, message.TrimEnd(), false);
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
            var balances = await nxtConnector.GetBalances(account.NxtAccountRs, transferables.NxtTransferables);
            foreach (var balance in balances)
            {
                if (balance.Value > 0 || balance.Key == Nxt.Singleton)
                {
                    if (transferables.ContainsTransferable(balance.Key))
                    {
                        message += MessageConstants.CurrentBalance(balance.Value, balance.Key, false) + "\n";
                    }
                    else
                    {
                        message += MessageConstants.CurrentBalance(balance.Value, balance.Key, true) + "\n";
                    }
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

        private async Task<bool> VerifyParameters(NxtTransferable transferable, string unit, NxtAccount account, string slackSessionId, decimal amount, int recipientCount = 1)
        {
            if (transferable == null)
            {
                await SlackConnector.SendMessage(slackSessionId, MessageConstants.UnknownUnit(unit));
                return false;
            }

            amount *= recipientCount;
            var nxtBalance = await nxtConnector.GetBalance(Nxt.Singleton, account.NxtAccountRs);
            if (transferable == Nxt.Singleton)
            {
                if (nxtBalance >= amount && nxtBalance < amount + 1 && recipientCount == 1)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFundsNeedFee(nxtBalance));
                    return false;
                }
                if (nxtBalance < amount + recipientCount)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFunds(nxtBalance, transferable.Name));
                    return false;
                }
            }
            else
            {
                if (nxtBalance < recipientCount)
                {
                    await SlackConnector.SendMessage(slackSessionId, MessageConstants.NotEnoughFundsNeedFee(nxtBalance, recipientCount));
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
            var transferable = transferables.GetTransferable(unit);
            var account = await walletRepository.GetAccount(slackUser.Id);

            if (account == null)
            {
                await SlackConnector.SendMessage(imSession.Id, MessageConstants.NoAccount);
                return;
            }
            if (transferable == null && unit.IsNumeric())
            {
                var id = ulong.Parse(unit);
                try
                {
                    var asset = await nxtConnector.GetAsset(new TransferableConfig(id, "", "", new List<string>()));
                    transferable = asset;
                }
                catch (Exception)
                {
                    try
                    {
                        var currency = await nxtConnector.GetCurrency(new TransferableConfig(id, "", "", new List<string>()));
                        transferable = currency;
                    }
                    catch (Exception) { }
                }
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
            var regex = new Regex("^\\s*(?i)withdraw(?-i) +(NXT-[A-Z0-9\\-]+) +([0-9]+\\.?[0-9]*) *([A-Za-z0-9_\\.]+)?");
            var match = regex.Match(message);
            return match;
        }

        private List<string> GetSlackUserIds(string input)
        {
            var userIds = new List<string>();
            var regex = new Regex("[\\s,?]*(?:<@([A-Za-z0-9]+)>)+");
            var matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                userIds.Add(match.Groups[1].Value);
            }
            return userIds;
        }

        private async Task Tip(SlackUser slackUser, Match match, SlackChannelSession channelSession)
        {
            var recipient = match.Groups[2].Value;
            var slackUserIds = GetSlackUserIds(recipient);
            var isRecipientSlackUser = slackUserIds.Any();
            var recipientCount = isRecipientSlackUser ? slackUserIds.Count : 1;
            var amountToTip = decimal.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var unit = string.IsNullOrEmpty(match.Groups["unit"].Value) ? Nxt.Singleton.Name : match.Groups["unit"].Value;
            var comment = string.IsNullOrEmpty(match.Groups["comment"].Value) ? string.Empty : match.Groups["comment"].Value;
            var transferable = transferables.GetTransferable(unit);
            var account = await walletRepository.GetAccount(slackUser.Id);

            if (slackUserIds.Contains(SlackConnector.SelfId))
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.CantTipBotChannel);
                return;
            }
            if (slackUserIds.Contains(slackUser.Id))
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.CantTipYourselfChannel);
                return;
            }
            if (!isRecipientSlackUser && !nxtConnector.IsValidAddressRs(recipient))
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.InvalidAddress);
                return;
            }
            if (account == null)
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.NoAccountChannel);
                return;
            }
            if (!string.IsNullOrEmpty(comment) && comment.Length > 512)
            {
                await SlackConnector.SendMessage(channelSession.Id, MessageConstants.CommentTooLongChannel);
                return;
            }
            if (!(await VerifyParameters(transferable, unit, account, channelSession.Id, amountToTip, recipientCount)))
            {
                return;
            }

            foreach (var slackUserId in slackUserIds)
            {
                var recipientAccount = await walletRepository.GetAccount(slackUserId);
                var recipientPublicKey = "";
                if (recipientAccount == null)
                {
                    recipientAccount = await SendTipRecievedInstantMessage(slackUser, slackUserId);
                    recipientPublicKey = recipientAccount.NxtPublicKey;
                }

                try
                {
                    var recipientUserName = SlackConnector.GetUser(slackUserId).Name;
                    var txMessage = MessageConstants.NxtTipTransactionMessage(slackUser.Name, recipientUserName, comment);
                    var txId = await nxtConnector.Transfer(account, recipientAccount.NxtAccountRs, transferable, amountToTip, txMessage, recipientPublicKey);
                    var reply = MessageConstants.TipSentChannel(slackUser.Id, slackUserId, amountToTip, transferable.Name, txId, comment);
                    await SlackConnector.SendMessage(channelSession.Id, reply, false);
                    await SendTransferableRecipientMessage(slackUser, slackUserId, transferable, recipientAccount, amountToTip);
                    await SendTransferableSenderMessage(slackUser, slackUserId, transferable, recipientAccount);
                }
                catch (NxtException e)
                {
                    logger.LogError(0, e, e.Message);
                    throw;
                }
            }
            if (!isRecipientSlackUser)
            {
                try
                {
                    var txMessage = MessageConstants.NxtTipTransactionMessage(slackUser.Name, "", comment);
                    var txId = await nxtConnector.Transfer(account, recipient, transferable, amountToTip, txMessage, "");
                    var reply = MessageConstants.TipToAddressRsSentChannel(slackUser.Id, recipient, amountToTip, transferable.Name, txId, comment);
                    await SlackConnector.SendMessage(channelSession.Id, reply, false);
                }
                catch (NxtException e)
                {
                    logger.LogError(0, e, e.Message);
                    throw;
                }
            }
        }

        private async Task SendTransferableSenderMessage(SlackUser slackUser, string recipient, NxtTransferable transferable, NxtAccount recipientAccount)
        {
            if (transferable != Nxt.Singleton)
            {
                var balance = await nxtConnector.GetBalance(Nxt.Singleton, recipientAccount.NxtAccountRs);
                if (balance < 1)
                {
                    var imId = await SlackConnector.GetInstantMessageId(slackUser.Id);
                    var message = MessageConstants.RecipientDoesNotHaveAnyNxtHint(recipientAccount.SlackId, transferable.Name);
                    await SlackConnector.SendMessage(imId, message);
                }
            }
        }

        private async Task SendTransferableRecipientMessage(SlackUser slackUser, string recipientUserId, NxtTransferable transferable, NxtAccount recipientAccount, decimal amount)
        {
            if (transferable.HasRecipientMessage())
            {
                var balance = await nxtConnector.GetBalance(transferable, recipientAccount.NxtAccountRs);
                if (balance == 0)
                {
                    var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
                    var message = transferable.RecipientMessage.Replace("{amount}", $"{amount}").Replace("{sender}", $"<@{slackUser.Id}>");
                    await SlackConnector.SendMessage(imId, message);
                }
            }
        }

        private async Task<NxtAccount> SendTipRecievedInstantMessage(SlackUser slackUser, string recipientUserId)
        {
            var recipientAccount = await CreateNxtAccount(recipientUserId);
            var imId = await SlackConnector.GetInstantMessageId(recipientUserId);
            await SlackConnector.SendMessage(imId, MessageConstants.TipRecieved(slackUser.Id));
            return recipientAccount;
        }

        private Match IsTipCommand(string message)
        {
            var regex = new Regex($"^\\s*(?i)({SlackConnector.SelfName}|<@{SlackConnector.SelfId}>) +tip(?-i) +((\\s*,?\\s*<@[A-Za-z0-9]+>){{0,5}}|NXT-[A-Z0-9\\-]+) +(?<amount>[0-9]+\\.?[0-9]*) *(?<unit>[A-Za-z0-9_\\.]+)? *(?<comment>.*)");
            var match = regex.Match(message);
            return match;
        }
    }
}