﻿using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NxtTipbot.Model;
using System.Collections.Generic;

namespace NxtTipbot.Tests
{
    public class SlackHandlerTests
    {
        private readonly Mock<INxtConnector> nxtConnectorMock = new Mock<INxtConnector>(); 
        private readonly Mock<IWalletRepository> walletRepositoryMock = new Mock<IWalletRepository>();
        private readonly Mock<ILogger> loggerMock = new Mock<ILogger>();
        private readonly Transferables transferables = new Transferables();
        private readonly Mock<ISlackConnector> slackConnectorMock = new Mock<ISlackConnector>();
        private readonly SlackHandler slackHandler;
        private readonly SlackIMSession imSession = new SlackIMSession { Id = "imSessionId", UserId = "SlackUserId" };
        private readonly SlackChannelSession channelSession = new SlackChannelSession { Id = "channelSessionId", Name = "#general" };
        private readonly SlackUser slackUser = new SlackUser { Id = "SlackUserId", Name = "XunitBot" };
        private readonly SlackUser recipientUser = new SlackUser { Id = TestConstants.RecipientAccount.SlackId, Name = "RecipientAccount" };
        private readonly SlackUser recipientUser2 = new SlackUser { Id = TestConstants.RecipientAccount2.SlackId, Name = "RecipientAccount2" };
        private readonly string botUserId = "botUserId";
        private readonly string botUserName = "tipper";
        private readonly ulong txId = 9837425;

        public SlackHandlerTests()
        {
            slackConnectorMock.SetupGet(c => c.SelfId).Returns(botUserId);
            slackConnectorMock.SetupGet(c => c.SelfName).Returns(botUserName);
            slackConnectorMock.Setup(c => c.GetUser(It.Is<string>(recipient => string.Equals(recipient, TestConstants.RecipientAccount.SlackId)))).Returns(recipientUser);
            slackConnectorMock.Setup(c => c.GetUser(It.Is<string>(recipient => string.Equals(recipient, TestConstants.RecipientAccount2.SlackId)))).Returns(recipientUser2);
            slackHandler = new SlackHandler(nxtConnectorMock.Object, walletRepositoryMock.Object, transferables, loggerMock.Object);
            slackHandler.SlackConnector = slackConnectorMock.Object;
            nxtConnectorMock.Setup(c => c.IsValidAddressRs(It.Is<string>(a => a == TestConstants.ValidAddressRs1))).Returns(true);
            nxtConnectorMock.Setup(c => c.IsValidAddressRs(It.Is<string>(a => a == TestConstants.InvalidAddressRs1))).Returns(false);
        }

        [Theory]
        [InlineData("help")]
        [InlineData(" HELP ")]
        [InlineData("hElP ")]
        public async void Help(string command)
        {
            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.GetHelpText(botUserName))), true));
        }

        [Theory]
        [InlineData("list")]
        [InlineData(" LIsT ")]
        [InlineData("liST ")]
        public async void List(string command)
        {
            transferables.AddTransferable(TestConstants.Asset);
            var expected = MessageConstants.ListCommandHeader + 
                MessageConstants.ListCommandForTransferable(Nxt.Singleton) +
                MessageConstants.ListCommandForTransferable(TestConstants.Asset).TrimEnd();

            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.StartsWith(MessageConstants.ListCommandHeader)), false));
            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Contains(MessageConstants.ListCommandForTransferable(Nxt.Singleton).TrimEnd())), false));
            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Contains(MessageConstants.ListCommandForTransferable(TestConstants.Asset).TrimEnd())), false));
        }

        [Theory]
        [InlineData("balance")]
        [InlineData(" BALANCE ")]
        [InlineData("bAlAnCe ")]
        public async void BalanceShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectNxtBalance()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(TestConstants.SenderAccount, expectedBalance);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance, Nxt.Singleton, false))), true));
        }

        [Fact]
        public async void BalanceShouldReturnZeroNxtBalance()
        {
            const decimal expectedBalance = 0M;
            SetupNxtAccount(TestConstants.SenderAccount, expectedBalance);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance, Nxt.Singleton, false))), true));
        }

        [Fact]
        public async void BalanceShouldReturnCorrectCurrencyBalance()
        {
            await BalanceShouldReturnCorrectTransferableBalance(TestConstants.Currency);
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectAssetBalance()
        {
            await BalanceShouldReturnCorrectTransferableBalance(TestConstants.Asset);
        }

        private async Task BalanceShouldReturnCorrectTransferableBalance(NxtTransferable transferable)
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(TestConstants.SenderAccount, 1);
            SetupTransferable(transferable, expectedBalance, TestConstants.SenderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Contains(MessageConstants.CurrentBalance(expectedBalance, transferable, false))), true));
        }

        [Fact]
        private async Task BalanceShouldReturnUnsupportedAssets()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(TestConstants.SenderAccount, 1);
            SetupTransferable(TestConstants.Asset, expectedBalance, TestConstants.SenderAccount.NxtAccountRs, false);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Contains(MessageConstants.CurrentBalance(expectedBalance, TestConstants.Asset, true))), true));
        }

        [Theory]
        [InlineData("deposit")]
        [InlineData(" DEPOSIT ")]
        [InlineData("dEpOsIt ")]
        public async void DepositShouldCreateAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);
            nxtConnectorMock.Setup(c => c.SetNxtProperties(It.IsAny<NxtAccount>()))
                .Callback((NxtAccount account) => account.NxtAccountRs = TestConstants.SenderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.AccountCreated(TestConstants.SenderAccount.NxtAccountRs))), true));
        }

        [Fact]
        public async void DepositShouldReturnAddress()
        {
            SetupNxtAccount(TestConstants.SenderAccount, 1);

            await slackHandler.InstantMessageCommand("deposit", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.DepositAddress(TestConstants.SenderAccount.NxtAccountRs))), true));
        }

        [Theory]
        [InlineData("withdraw")]
        [InlineData(" WITHDRAW")]
        [InlineData("wItHdRaW")]
        public async void WithdrawNxtShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageCommand($"{command} {TestConstants.RecipientAccount.NxtAccountRs} 42", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }

        [Fact]
        public async void WithdrawNxtShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(TestConstants.SenderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} 42", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Theory]
        [InlineData("4")]
        [InlineData("3.00001")]
        public async void WithdrawNxtShouldReturnYouNeedNxtForFee(string amount)
        {
            const decimal balance = 4;
            SetupNxtAccount(TestConstants.SenderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} {amount}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFundsNeedFee(balance, 1))), true));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" NXT")]
        public async void WithdrawNxtShouldSucceed(string unit)
        {
            const decimal withdrawAmount = 42;
            var message = $"withdraw {TestConstants.RecipientAccount.NxtAccountRs} {withdrawAmount}{unit}";
            await TryWithdrawNxt(message, withdrawAmount);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public async void WithdrawNxtShouldSucceed(int whiteSpaceCount)
        {
            var whiteSpaces = new string(' ', whiteSpaceCount);
            const decimal withdrawAmount = 42;
            var message = $"withdraw{whiteSpaces}{TestConstants.RecipientAccount.NxtAccountRs}{whiteSpaces}{withdrawAmount}{whiteSpaces}NXT";
            await TryWithdrawNxt(message, withdrawAmount);
        }

        public async Task TryWithdrawNxt(string message, decimal withdrawAmount)
        {
            const decimal balance = 400;
            const ulong txId = 928347;
            SetupNxtAccount(TestConstants.SenderAccount, balance);
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount),
                It.Is<string>(r => r == TestConstants.RecipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(t => t == Nxt.Singleton),
                It.Is<decimal>(amount => amount == withdrawAmount),
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageCommand(message, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, "NXT", txId))), false));
        }

        [Fact]
        public async void WithdrawShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(TestConstants.SenderAccount, 1);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} 42 {unknownUnit}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughNxtFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Currency);
        }

        [Fact]
        public async void WithdrawAssetShouldReturnNotEnoughNxtFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Asset);
        }

        private async Task WithdrawTransferableShouldReturnNotEnoughNxtFunds(NxtTransferable transferable)
        {
            const decimal balance = 0.9M;
            transferables.AddTransferable(transferable);
            SetupNxtAccount(TestConstants.SenderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} 42 {transferable.Name}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFundsNeedFee(balance, 1))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(TestConstants.Currency);
        }

        [Fact]
        public async void WithdrawAssetShouldReturnNotEnoughCurrencyFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(TestConstants.Asset);
        }

        private async Task WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(NxtTransferable transferable)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 1M;
            SetupNxtAccount(TestConstants.SenderAccount, nxtBalance);
            SetupTransferable(transferable, balance, TestConstants.SenderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} 42 {transferable.Name}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, transferable.Name))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldSucceed()
        {
            await WithdrawTransferableShouldSucceed(TestConstants.Currency, TestConstants.Currency.Name);
        }

        [Fact]
        public async void WithdrawAssetShouldSucceed()
        {
            await WithdrawTransferableShouldSucceed(TestConstants.Asset, TestConstants.Asset.Name);
        }
        
        [Fact]
        public async void WithdrawAssetShouldSucceedWhenUsingMoniker()
        {
            await WithdrawTransferableShouldSucceed(TestConstants.Asset, TestConstants.Asset.Monikers[0]);
        }

        [Fact]
        public async void WithdrawAssetShouldSucceedWhenUsingId()
        {
            await WithdrawTransferableShouldSucceed(TestConstants.Asset, TestConstants.Asset.Id.ToString());
        }

        [Fact]
        public async void WithdrawAssetShouldSucceedWhenAssetIsUnsupported()
        {
            var assetId = TestConstants.Asset.Id.ToString();
            nxtConnectorMock.Setup(c => c.GetAsset(It.Is<TransferableConfig>(tc => tc.Id.ToString() == assetId))).ReturnsAsync(TestConstants.Asset);

            await WithdrawTransferableShouldSucceed(TestConstants.Asset, assetId, false);
        }

        private async Task WithdrawTransferableShouldSucceed(NxtTransferable transferable, string unit, bool supportedTransferable = true)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 100M;
            const decimal withdrawAmount = 42;
            SetupNxtAccount(TestConstants.SenderAccount, nxtBalance);
            SetupTransferable(transferable, balance, TestConstants.SenderAccount.NxtAccountRs, supportedTransferable);
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount), 
                It.Is<string>(r => r == TestConstants.RecipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(tran => tran == transferable),
                It.Is<decimal>(amount => amount == withdrawAmount), 
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageCommand($"withdraw {TestConstants.RecipientAccount.NxtAccountRs} {withdrawAmount} {unit}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, transferable.Name, txId))), false));
        }

        [Theory]
        [InlineData("tipper tip")]
        [InlineData(" TIPPER TIP")]
        [InlineData("tIpPeR tIp")]
        public async void TipShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.Is<string>(a => a == slackUser.Id))).ReturnsAsync(null);
            var message = CreateChannelMessage($"{command} <@{TestConstants.RecipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccountChannel)), true));
        }

        [Fact]
        public async void TipShouldReturnCantTipBot()
        {
            var message = CreateChannelMessage($"tipper tip <@{botUserId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.CantTipBotChannel)), true));
        }

        [Fact]
        public async void TipShouldReturnCantTipYourself()
        {
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.SenderAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.CantTipYourselfChannel)), true));
        }

        [Fact]
        public async void TipNxtShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(TestConstants.SenderAccount, balance);
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Theory]
        [InlineData("4")]
        [InlineData("3.00001")]
        public async void TipNxtShouldReturnYouNeedNxtForFee(string amount)
        {
            const decimal balance = 4;
            SetupNxtAccount(TestConstants.SenderAccount, balance);
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> {amount}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFundsNeedFee(balance, 1))), true));
        }

        [Fact]
        public async void TipNxtShouldFailWithTooLongComment()
        {
            var comment = new string('.', 2000);
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42 NXT {comment}");
            SetupNxtAccount(TestConstants.SenderAccount, 500);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.CommentTooLongChannel)), true));
        }

        [Fact]
        public async void TipNxtToAddressShouldFailWithInvalidAddress()
        {
            var message = CreateChannelMessage($"tipper tip {TestConstants.InvalidAddressRs1} 42 NXT");
            SetupNxtAccount(TestConstants.SenderAccount, 500);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.InvalidAddress)), true));
        }

        [Fact]
        public async void TipNxtToAddressShouldSucceed()
        {
            const decimal tipAmount = 42;
            var message = CreateChannelMessage($"tipper tip {TestConstants.ValidAddressRs1} 42 NXT");
            await SendSuccessfulTip(message, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipToAddressRsSentChannel(slackUser.Id, TestConstants.ValidAddressRs1, tipAmount, "NXT", txId, ""))), false));
        }

        [Fact]
        public async void TipShouldSucceedOnUserIdUsage()
        {
            const decimal tipAmount = 42;
            var message = CreateChannelMessage($"<@{botUserId}> tip <@{TestConstants.RecipientAccount.SlackId}> {tipAmount}");
            await SendSuccessfulTip(message, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, TestConstants.RecipientAccount.SlackId, tipAmount, "NXT", txId, ""))), false));
        }

        [Fact]
        public async void TipShouldFailOnMixedRecipientTypes()
        {
            const decimal tipAmount = 42;
            var message = CreateChannelMessage($"<@{botUserId}> tip <@{TestConstants.RecipientAccount.SlackId}>, {TestConstants.RecipientAccount2.NxtAccountRs} {tipAmount}");
            
            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, MessageConstants.UnknownChannelCommandReply, true));
        }

        [Fact]
        public async void TipNxtShouldReturnNotEnoughFundsOnMultipleRecipients()
        {
            const decimal tipAmount = 42;
            const decimal balance = 50;
            var message = CreateChannelMessage($"<@{botUserId}> tip <@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}> {tipAmount}");
            SetupNxtAccount(TestConstants.SenderAccount, balance);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void TipAssetShouldReturnNotEnoughNxtFundsOnMultipleRecipients()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Asset, $"<@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}>", 2, 1.9M);
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughNxtFundsOnMultipleRecipients()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Currency, $"<@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}>", 2, 1.9M);
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughCurrencyFundsOnMultipleRecipients()
        {
            await TipTransferableShouldReturnNotEnoughTransferableFunds(TestConstants.Currency, $"<@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}>", 50M);
        }

        [Fact]
        public async void TipAssetShouldReturnNotEnoughAssetFundsOnMultipleRecipients()
        {
            await TipTransferableShouldReturnNotEnoughTransferableFunds(TestConstants.Asset, $"<@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}>", 50M);
        }

        [Fact]
        public async void TipShouldSucceedOnMultipleRecipients()
        {
            const decimal tipAmount = 42;
            const decimal balance = 400;
            var recipients = $"<@{TestConstants.RecipientAccount.SlackId}>, <@{TestConstants.RecipientAccount2.SlackId}>";
            var txId2 = txId + 1;
            var message = CreateChannelMessage($"<@{botUserId}> tip {recipients} {tipAmount}");

            SetupNxtAccount(TestConstants.SenderAccount, balance);
            SetupNxtAccount(TestConstants.RecipientAccount, 0);
            SetupNxtAccount(TestConstants.RecipientAccount2, 0);

            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount),
                It.Is<string>(r => r == TestConstants.RecipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(t => t == Nxt.Singleton),
                It.Is<decimal>(amount => amount == tipAmount),
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount),
                It.Is<string>(r => r == TestConstants.RecipientAccount2.NxtAccountRs),
                It.Is<NxtTransferable>(t => t == Nxt.Singleton),
                It.Is<decimal>(amount => amount == tipAmount),
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId2);

            slackConnectorMock.Setup(c => c.GetInstantMessageId(It.Is<string>(id => id == TestConstants.RecipientAccount.SlackId))).ReturnsAsync(imSession.Id);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.MultitipSentChannel(slackUser.Id, recipients, tipAmount, "NXT", ""))), true));
            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentDirectMessage(slackUser.Id, tipAmount, "NXT", txId))), true));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" NXT")]
        public async void TipNxtShouldSucceed(string unit)
        {
            const decimal tipAmount = 42;
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42{unit}");
            await SendSuccessfulTip(message, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, TestConstants.RecipientAccount.SlackId, tipAmount, "NXT", txId, ""))), false));
        }

        [Fact]
        public async void TipNxtShouldSucceedWithComment()
        {
            const decimal tipAmount = 42;
            const string comment = "here ya go! :)";
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42 NXT {comment}");
            await SendSuccessfulTip(message, tipAmount, "");

            nxtConnectorMock.Verify(c => c.Transfer(
                It.IsAny<NxtAccount>(),
                It.IsAny<string>(),
                It.IsAny<NxtTransferable>(),
                It.IsAny<decimal>(),
                It.Is<string>(msg => msg.EndsWith(comment)),
                It.IsAny<string>()));
        }

        [Fact]
        public async void TipNxtShouldSucceedWithRecipientAndSenderInTransactionMessage()
        {
            const decimal tipAmount = 42;
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42 NXT");
            var transactionMessage = MessageConstants.NxtTipTransactionMessage(slackUser.Name, recipientUser.Name, "");

            await SendSuccessfulTip(message, tipAmount, "");

            nxtConnectorMock.Verify(c => c.Transfer(
                It.IsAny<NxtAccount>(),
                It.IsAny<string>(),
                It.IsAny<NxtTransferable>(),
                It.IsAny<decimal>(),
                It.Is<string>(msg => msg.Equals(transactionMessage)),
                It.IsAny<string>()));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public async void TipNxtShouldSucceedWithMultipleWhiteSpaces(int whiteSpaceCount)
        {
            const decimal tipAmount = 42;
            var spaces = new string(' ', whiteSpaceCount);
            var message = CreateChannelMessage($"tipper{spaces}tip{spaces}<@{TestConstants.RecipientAccount.SlackId}>{spaces}42");
            await SendSuccessfulTip(message, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, TestConstants.RecipientAccount.SlackId, tipAmount, "NXT", txId, ""))), false));
        }

        private async Task SendSuccessfulTip(SlackMessage message, decimal tipAmount, string comment = "")
        {
            const decimal balance = 400;
            SetupNxtAccount(TestConstants.SenderAccount, balance);
            SetupNxtAccount(TestConstants.RecipientAccount, 0);
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount),
                It.Is<string>(r => r == TestConstants.RecipientAccount.NxtAccountRs || r == TestConstants.ValidAddressRs1),
                It.Is<NxtTransferable>(t => t == Nxt.Singleton),
                It.Is<decimal>(amount => amount == tipAmount),
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);
        }

        [Fact]
        public async void TipShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(TestConstants.SenderAccount, 1);
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42 {unknownUnit}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughNxtFunds()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Currency, $"<@{TestConstants.RecipientAccount.SlackId}>");
        }

        [Fact]
        public async void TipAssetShouldReturnNotEnoughNxtFunds()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(TestConstants.Asset, $"<@{TestConstants.RecipientAccount.SlackId}>");
        }

        private async Task TipTransferableShouldReturnNotEnoughNxtFunds(NxtTransferable transferable, string recipient, int recipientCount = 1, decimal nxtBalance = 0.9M)
        {
            transferables.AddTransferable(transferable);
            SetupNxtAccount(TestConstants.SenderAccount, nxtBalance);
            var message = CreateChannelMessage($"tipper tip {recipient} 42 {transferable.Name}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFundsNeedFee(nxtBalance, recipientCount))), true));
        }
        
        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            await TipTransferableShouldReturnNotEnoughTransferableFunds(TestConstants.Currency, $"<@{TestConstants.RecipientAccount.SlackId}>");
        }
        
        [Fact]
        public async void TipAssetShouldReturnNotEnoughAssetFunds()
        {
            await TipTransferableShouldReturnNotEnoughTransferableFunds(TestConstants.Asset, $"<@{TestConstants.RecipientAccount.SlackId}>");
        }

        private async Task TipTransferableShouldReturnNotEnoughTransferableFunds(NxtTransferable transferable, string recipient, decimal balance = 1M)
        {
            const decimal nxtBalance = 100M;
            SetupNxtAccount(TestConstants.SenderAccount, nxtBalance);
            SetupTransferable(transferable, balance, TestConstants.SenderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipper tip {recipient} 42 {transferable.Name}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, transferable.Name))), true));
        }

        [Fact]
        public async void TipCurrencyShouldSucceedWithComment()
        {
            const decimal tipAmount = 42;
            const string comment = "here ya go! :)";
            await SetupSuccessfulTipTransferable(TestConstants.Currency, TestConstants.Currency.Name, tipAmount, comment);

            nxtConnectorMock.Verify(c => c.Transfer(
                It.IsAny<NxtAccount>(), 
                It.IsAny<string>(), 
                It.IsAny<NxtTransferable>(), 
                It.IsAny<decimal>(),
                It.Is<string>(msg => msg.EndsWith(comment)), 
                It.IsAny<string>()));
        }

        [Fact]
        private async void TipCurrencyShouldSucceed()
        {
            const decimal tipAmount = 42;
            await SetupSuccessfulTipTransferable(TestConstants.Currency, TestConstants.Currency.Name, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, 
                TestConstants.RecipientAccount.SlackId, tipAmount, TestConstants.Currency.Name, txId, ""))), false));
        }

        [Fact]
        private async void TipAssetUsingMonikerShouldSucceed()
        {
            await TipAssetUsingUnitNameShouldSucceed(TestConstants.Asset.Monikers[0]);
        }

        [Fact]
        private async void TipAssetUsingIdShouldSucceed()
        {
            await TipAssetUsingUnitNameShouldSucceed(TestConstants.Asset.Id.ToString());
        }

        private async Task TipAssetUsingUnitNameShouldSucceed(string unitName)
        {
            const decimal tipAmount = 42;
            await SetupSuccessfulTipTransferable(TestConstants.Asset, unitName, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id,
                TestConstants.RecipientAccount.SlackId, tipAmount, TestConstants.Asset.Name, txId, ""))), false));
        }

        [Fact]
        private async void TipAssetShouldSucceed()
        {
            const decimal tipAmount = 42;
            await SetupSuccessfulTipTransferable(TestConstants.Asset, TestConstants.Asset.Name, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id,
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, 
                TestConstants.RecipientAccount.SlackId, tipAmount, TestConstants.Asset.Name, txId, ""))), false));
        }

        [Fact]
        public async void TipCurrencyShouldSendMessageToSenderWhenRecipientHasZeroNxt()
        {
            await TipTransferrableShouldSendMessageToSenderWhenRecipientHasZeroNxt(TestConstants.Currency);
        }

        [Fact]
        public async void TipAssetShouldSendMessageToSenderWhenRecipientHasZeroNxt()
        {
            await TipTransferrableShouldSendMessageToSenderWhenRecipientHasZeroNxt(TestConstants.Asset);
        }

        private async Task TipTransferrableShouldSendMessageToSenderWhenRecipientHasZeroNxt(NxtTransferable transferable)
        {
            const decimal tipAmount = 42;
            var expectedMessage = MessageConstants.RecipientDoesNotHaveAnyNxtHint(TestConstants.RecipientAccount.SlackId, transferable.Name);
            slackConnectorMock.Setup(c => c.GetInstantMessageId(It.Is<string>(id => id == TestConstants.SenderAccount.SlackId))).ReturnsAsync(imSession.Id);

            await SetupSuccessfulTipTransferable(transferable, transferable.Name, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, expectedMessage, true));
        }

        [Fact]
        public async void TipAssetShouldSendMessageToRecipient()
        {
            await TipTransferableShouldSendMessageToRecipient(TestConstants.Asset);
        }

        [Fact]
        public async void TipCurrencyShouldSendMessageToRecipient()
        {
            await TipTransferableShouldSendMessageToRecipient(TestConstants.Currency);
        }

        private async Task TipTransferableShouldSendMessageToRecipient(NxtTransferable transferable)
        {
            const decimal tipAmount = 42;
            var expectedMessage = transferable.RecipientMessage.Replace("{sender}", $"<@{TestConstants.SenderAccount.SlackId}>").Replace("{amount}", $"{tipAmount}");
            slackConnectorMock.Setup(c => c.GetInstantMessageId(It.Is<string>(id => id == recipientUser.Id))).ReturnsAsync(imSession.Id);

            await SetupSuccessfulTipTransferable(transferable, transferable.Name, tipAmount);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, expectedMessage, true));
        }

        private async Task SetupSuccessfulTipTransferable(NxtTransferable transferable, string unit, decimal tipAmount, string comment = "")
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 100M;
            SetupNxtAccount(TestConstants.SenderAccount, nxtBalance);
            SetupNxtAccount(TestConstants.RecipientAccount, 0);
            SetupTransferable(transferable, balance, TestConstants.SenderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipper tip <@{TestConstants.RecipientAccount.SlackId}> 42 {unit} {comment}");
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == TestConstants.SenderAccount), 
                It.Is<string>(r => r == TestConstants.RecipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(tran => tran == transferable),
                It.Is<decimal>(amount => amount == tipAmount), 
                It.IsAny<string>(),
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);
        }

        private SlackMessage CreateChannelMessage(string text)
        {
            return new SlackMessage
            {
                ChannelId = channelSession.Id,
                UserId = slackUser.Id,
                Text = text
            };
        }

        private void SetupNxtAccount(NxtAccount nxtAccount, decimal balance)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.Is<string>(slackId => slackId == nxtAccount.SlackId))).ReturnsAsync(nxtAccount);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtTransferable>(t => t == Nxt.Singleton), It.Is<string>(rs => rs == nxtAccount.NxtAccountRs))).ReturnsAsync(balance);
            SetupTransferable(Nxt.Singleton, balance, nxtAccount.NxtAccountRs);
        }

        private Dictionary<string, Dictionary<NxtTransferable, decimal>> balances = new Dictionary<string, Dictionary<NxtTransferable, decimal>>();
        private void SetupTransferable(NxtTransferable transferable, decimal balance, string accountRs, bool addToTransferables = true)
        {
            if (transferable != Nxt.Singleton && addToTransferables)
            {
                transferables.AddTransferable(transferable);
            }

            Dictionary<NxtTransferable, decimal> accountBalances;
            if (!balances.TryGetValue(accountRs, out accountBalances))
            {
                accountBalances = new Dictionary<NxtTransferable, decimal>();
                balances.Add(accountRs, accountBalances);
            }

            accountBalances.Add(transferable, balance);

            nxtConnectorMock.Setup(connector => connector.GetBalance(
                It.Is<NxtTransferable>(t => t == transferable),
                It.Is<string>(a => a == accountRs)))
                    .ReturnsAsync(balance);

            nxtConnectorMock.Setup(connector => connector.GetBalances(
                It.Is<string>(a => a == accountRs),
                It.IsAny<IList<NxtTransferable>>()))
                    .ReturnsAsync(accountBalances);
        }
    }
}
