using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using NxtLib.MonetarySystem;
using NxtLib;
using NxtLib.AssetExchange;
using System.Threading.Tasks;

namespace NxtTipbot.Tests
{
    public class SlackHandlerTests
    {
        private readonly Mock<INxtConnector> nxtConnectorMock = new Mock<INxtConnector>(); 
        private readonly Mock<IWalletRepository> walletRepositoryMock = new Mock<IWalletRepository>();
        private readonly Mock<ILogger> loggerMock = new Mock<ILogger>();
        private readonly Mock<ISlackConnector> slackConnectorMock = new Mock<ISlackConnector>();
        private readonly SlackHandler slackHandler;
        private readonly SlackIMSession imSession = new SlackIMSession { Id = "imSessionId", UserId = "SlackUserId" };
        private readonly SlackChannelSession channelSession = new SlackChannelSession { Id = "channelSessionId", Name = "#general" };
        private readonly SlackUser slackUser = new SlackUser { Id = "SlackUserId", Name = "XunitBot" };
        private readonly NxtAccount senderAccount = new NxtAccount
        {
            Id = 42,
            SlackId = "SlackUserId",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-8MVA-XCVR-3JC9-2C7C3"
        };
        private readonly NxtAccount recipientAccount = new NxtAccount
        {
            Id = 43,
            SlackId = "RecipientUserId",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-K5KL-23DJ-3XLK-22222"
        };
        private readonly NxtCurrency currency = new NxtCurrency(new Currency
        {
            CurrencyId = 123,
            Code = "TEST",
            Decimals = 4
        });
        private readonly NxtAsset asset = new NxtAsset(new Asset
        {
            AssetId = 123,
            Decimals = 4
        }, "TEST");

        public SlackHandlerTests()
        {
            slackHandler = new SlackHandler(nxtConnectorMock.Object, walletRepositoryMock.Object, loggerMock.Object);
            slackHandler.SlackConnector = slackConnectorMock.Object;
        }

        [Theory]
        [InlineData("help")]
        [InlineData(" HELP ")]
        [InlineData("hElP ")]
        public async void Help(string command)
        {
            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.HelpText)), true));
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
            SetupNxtAccount(senderAccount, expectedBalance);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance))), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectCurrencyBalance()
        {
            await BalanceShouldReturnCorrectTransferableBalance(currency);
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectAssetBalance()
        {
            await BalanceShouldReturnCorrectTransferableBalance(asset);
        }
        
        private async Task BalanceShouldReturnCorrectTransferableBalance(NxtTransferable transferable)
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(senderAccount, 1);
            SetupTransferable(transferable, expectedBalance, senderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand("balance", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Contains(MessageConstants.CurrentBalance(expectedBalance, transferable.Name))), true));
        }

        [Theory]
        [InlineData("deposit")]
        [InlineData(" DEPOSIT ")]
        [InlineData("dEpOsIt ")]
        public async void DepositShouldCreateAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);
            nxtConnectorMock.Setup(c => c.CreateAccount(It.Is<string>(id => id == this.slackUser.Id))).Returns(senderAccount);

            await slackHandler.InstantMessageCommand(command, slackUser, imSession);

            walletRepositoryMock.Verify(r => r.AddAccount(senderAccount));
            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.AccountCreated(senderAccount.NxtAccountRs))), true));
        }

        [Fact]
        public async void DepositShouldReturnAddress()
        {
            SetupNxtAccount(senderAccount, 1);

            await slackHandler.InstantMessageCommand("deposit", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.DepositAddress(senderAccount.NxtAccountRs))), true));
        }

        [Theory]
        [InlineData("withdraw")]
        [InlineData(" WITHDRAW")]
        [InlineData("wItHdRaW")]
        public async void WithdrawNxtShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageCommand($"{command} {recipientAccount.NxtAccountRs} 42", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }

        [Fact]
        public async void WithdrawNxtShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(senderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" NXT")]
        public async void WithdrawNxtShouldSucceed(string unit)
        {
            const decimal balance = 400;
            const decimal withdrawAmount = 42;
            const ulong txId = 928347;
            SetupNxtAccount(senderAccount, balance);
            nxtConnectorMock.Setup(c => c.SendMoney(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs), 
                It.Is<Amount>(a => a.Nxt == withdrawAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} {withdrawAmount}{unit}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, "NXT", txId))), false));
        }

        [Fact]
        public async void WithdrawShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(senderAccount, 1);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {unknownUnit}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughNxtFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughNxtFunds(currency);
        }

        [Fact]
        public async void WithdrawAssetShouldReturnNotEnoughNxtFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughNxtFunds(asset);
        }

        private async Task WithdrawTransferableShouldReturnNotEnoughNxtFunds(NxtTransferable transferable)
        {
            const decimal balance = 0.9M;
            slackHandler.AddTransferable(transferable);
            SetupNxtAccount(senderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {transferable.Name}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(currency);
        }

        [Fact]
        public async void WithdrawAssetShouldReturnNotEnoughCurrencyFunds()
        {
            await WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(asset);
        }

        private async Task WithdrawTransferableShouldReturnNotEnoughCurrencyFunds(NxtTransferable transferable)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 1M;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupTransferable(transferable, balance, senderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {transferable.Name}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, transferable.Name))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldSucceed()
        {
            await WithdrawTransferableShouldSucceed(currency);
        }

        [Fact]
        public async void WithdrawAssetShouldSucceed()
        {
            await WithdrawTransferableShouldSucceed(asset);
        }

        private async Task WithdrawTransferableShouldSucceed(NxtTransferable transferable)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 100M;
            const decimal withdrawAmount = 42;
            const ulong txId = 9837425;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupTransferable(transferable, balance, senderAccount.NxtAccountRs);
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(tran => tran == transferable),
                It.Is<decimal>(amount => amount == withdrawAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} {withdrawAmount} {transferable.Name}", slackUser, imSession);

            slackConnectorMock.Verify(c => c.SendMessage(imSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, transferable.Name, txId))), false));
        }

        [Theory]
        [InlineData("tipbot tip")]
        [InlineData(" TIPBOT TIP")]
        [InlineData("tIpBoT tIp")]
        public async void TipShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.Is<string>(a => a == slackUser.Id))).ReturnsAsync(null);
            var message = CreateChannelMessage($"{command} <@{recipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccountChannel)), true));
        }

        [Fact]
        public async void TipNxtShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(senderAccount, balance);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" NXT")]
        public async void TipNxtShouldSucceed(string unit)
        {
            const decimal balance = 400;
            const decimal tipAmount = 42;
            const ulong txId = 928347;
            SetupNxtAccount(senderAccount, balance);
            SetupNxtAccount(recipientAccount, 0);
            nxtConnectorMock.Setup(c => c.SendMoney(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs), 
                It.Is<Amount>(a => a.Nxt == tipAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42{unit}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, recipientAccount.SlackId, tipAmount, "NXT", txId))), false));
        }

        [Fact]
        public async void TipShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(senderAccount, 1);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {unknownUnit}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughNxtFunds()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(currency);
        }

        [Fact]
        public async void TipAssetShouldReturnNotEnoughNxtFunds()
        {
            await TipTransferableShouldReturnNotEnoughNxtFunds(asset);
        }

        private async Task TipTransferableShouldReturnNotEnoughNxtFunds(NxtTransferable transferable)
        {
            const decimal balance = 0.9M;
            slackHandler.AddTransferable(transferable);
            SetupNxtAccount(senderAccount, balance);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {transferable.Name}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }
        
        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            await TipTransferableShouldReturnNotEnoughCurrencyFunds(currency);
        }
        
        [Fact]
        public async void TipAssetShouldReturnNotEnoughCurrencyFunds()
        {
            await TipTransferableShouldReturnNotEnoughCurrencyFunds(asset);
        }

        private async Task TipTransferableShouldReturnNotEnoughCurrencyFunds(NxtTransferable transferable)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 1M;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupTransferable(transferable, balance, senderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {transferable.Name}");

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, transferable.Name))), true));
        }

        [Fact]
        private async void TipCurrencyShouldSucceed()
        {
            await TipTransferableShouldSucceed(currency);
        }

        [Fact]
        private async void TipAssetShouldSucceed()
        {
            await TipTransferableShouldSucceed(asset);
        }

        private async Task TipTransferableShouldSucceed(NxtTransferable transferable)
        {
            const decimal nxtBalance = 1M;
            const decimal balance = 100M;
            const decimal tipAmount = 42;
            const ulong txId = 9837425;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupNxtAccount(recipientAccount, 0);
            SetupTransferable(transferable, balance, senderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {transferable.Name}");
            nxtConnectorMock.Setup(c => c.Transfer(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs),
                It.Is<NxtTransferable>(tran => tran == transferable),
                It.Is<decimal>(amount => amount == tipAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.TipBotChannelCommand(message, slackUser, channelSession);

            slackConnectorMock.Verify(c => c.SendMessage(channelSession.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(slackUser.Id, recipientAccount.SlackId, tipAmount, transferable.Name, txId))), false));
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
            nxtConnectorMock.Setup(c => c.GetNxtBalance(It.Is<NxtAccount>(a => a == nxtAccount))).ReturnsAsync(balance);
        }

        private void SetupTransferable(NxtTransferable transferable, decimal balance, string accountRs)
        {
            slackHandler.AddTransferable(transferable);
            nxtConnectorMock.Setup(connector => connector.GetBalance(
                It.Is<NxtTransferable>(t => t == transferable), 
                It.Is<string>(a => a == accountRs)))
                    .ReturnsAsync(balance);
        }
    }
}
