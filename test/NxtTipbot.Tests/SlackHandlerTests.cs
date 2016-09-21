using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using NxtLib.MonetarySystem;
using NxtLib;
using System;

namespace NxtTipbot.Tests
{
    public class SlackHandlerTests
    {
        private readonly Mock<INxtConnector> nxtConnectorMock = new Mock<INxtConnector>(); 
        private readonly Mock<IWalletRepository> walletRepositoryMock = new Mock<IWalletRepository>();
        private readonly Mock<ILogger> loggerMock = new Mock<ILogger>();
        private readonly Mock<ISlackConnector> slackConnectorMock = new Mock<ISlackConnector>();
        private readonly SlackHandler slackHandler;
        private readonly InstantMessage instantMessage = new InstantMessage { Id = "InstantMessageId", UserId = "UserId" };
        private readonly Channel channel = new Channel { Id = "ChannelId", Name = "#general" };
        private readonly User user = new User { Id = "UserId", Name = "XunitBot" };
        private readonly NxtAccount senderAccount = new NxtAccount
        {
            Id = 42,
            SlackId = "UserId",
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
        private readonly Currency currency = new Currency
        {
            CurrencyId = 123,
            Code = "TEST",
            Decimals = 4
        };

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
            await slackHandler.InstantMessageCommand(command, user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.HelpText)), true));
        }
        
        [Theory]
        [InlineData("balance")]
        [InlineData(" BALANCE ")]
        [InlineData("bAlAnCe ")]
        public async void BalanceShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageCommand(command, user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectBalance()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(senderAccount, expectedBalance);

            await slackHandler.InstantMessageCommand("balance", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance))), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectCurrencyBalance()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(senderAccount, 1);
            SetupCurrency(currency, expectedBalance, senderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand("balance", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentCurrencyBalance(expectedBalance, currency.Code))), true));
        }

        [Theory]
        [InlineData("deposit")]
        [InlineData(" DEPOSIT ")]
        [InlineData("dEpOsIt ")]
        public async void DepositShouldCreateAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);
            nxtConnectorMock.Setup(c => c.CreateAccount(It.Is<string>(id => id == this.user.Id))).Returns(senderAccount);

            await slackHandler.InstantMessageCommand(command, user, instantMessage);

            walletRepositoryMock.Verify(r => r.AddAccount(senderAccount));
            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.AccountCreated(senderAccount.NxtAccountRs))), true));
        }

        [Fact]
        public async void DepositShouldReturnAddress()
        {
            SetupNxtAccount(senderAccount, 1);

            await slackHandler.InstantMessageCommand("deposit", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.DepositAddress(senderAccount.NxtAccountRs))), true));
        }

        [Theory]
        [InlineData("withdraw")]
        [InlineData(" WITHDRAW")]
        [InlineData("wItHdRaW")]
        public async void WithdrawShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageCommand($"{command} {recipientAccount.NxtAccountRs} 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }

        [Fact]
        public async void WithdrawShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(senderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
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

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} {withdrawAmount}{unit}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, "NXT", txId))), false));
        }

        [Fact]
        public async void WithdrawShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(senderAccount, 1);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {unknownUnit}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughNxtFunds()
        {
            const decimal balance = 0.9M;
            slackHandler.AddCurrency(currency);
            SetupNxtAccount(senderAccount, balance);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 1M;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupCurrency(currency, currencyBalance, senderAccount.NxtAccountRs);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} 42 {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(currencyBalance, currency.Code))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldSucceed()
        {
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 100M;
            const decimal withdrawAmount = 42;
            const ulong txId = 9837425;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupCurrency(currency, currencyBalance, senderAccount.NxtAccountRs);
            nxtConnectorMock.Setup(c => c.TransferCurrency(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs),
                It.Is<ulong>(cId => cId == currency.CurrencyId),
                It.Is<long>(units => units == (long)withdrawAmount * Math.Pow(currency.Decimals, 10)), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageCommand($"withdraw {recipientAccount.NxtAccountRs} {withdrawAmount} {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, currency.Code, txId))), false));
        }

        [Theory]
        [InlineData("tipbot tip")]
        [InlineData(" TIPBOT TIP")]
        [InlineData("tIpBoT tIp")]
        public async void TipShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.Is<string>(a => a == user.Id))).ReturnsAsync(null);
            var message = CreateChannelMessage($"{command} <@{recipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccountChannel)), true));
        }

        [Fact]
        public async void TipShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(senderAccount, balance);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42");

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
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

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(user.Id, recipientAccount.SlackId, tipAmount, "NXT", txId))), false));
        }

        [Fact]
        public async void TipShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(senderAccount, 1);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {unknownUnit}");

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughNxtFunds()
        {
            const decimal balance = 0.9M;
            slackHandler.AddCurrency(currency);
            SetupNxtAccount(senderAccount, balance);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {currency.Code}");

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void TipCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 1M;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupCurrency(currency, currencyBalance, senderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {currency.Code}");

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(currencyBalance, currency.Code))), true));
        }

        [Fact]
        public async void TipCurrencyShouldSucceed()
        {
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 100M;
            const decimal tipAmount = 42;
            const ulong txId = 9837425;
            SetupNxtAccount(senderAccount, nxtBalance);
            SetupNxtAccount(recipientAccount, 0);
            SetupCurrency(currency, currencyBalance, senderAccount.NxtAccountRs);
            var message = CreateChannelMessage($"tipbot tip <@{recipientAccount.SlackId}> 42 {currency.Code}");
            nxtConnectorMock.Setup(c => c.TransferCurrency(
                It.Is<NxtAccount>(a => a == senderAccount), 
                It.Is<string>(r => r == recipientAccount.NxtAccountRs),
                It.Is<ulong>(cId => cId == currency.CurrencyId),
                It.Is<long>(units => units == (long)tipAmount * Math.Pow(currency.Decimals, 10)), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.TipBotChannelCommand(message, user, channel);

            slackConnectorMock.Verify(c => c.SendMessage(channel.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.TipSentChannel(user.Id, recipientAccount.SlackId, tipAmount, currency.Code, txId))), false));
        }

        private Message CreateChannelMessage(string text)
        {
            return new Message
            {
                ChannelId = channel.Id,
                UserId = user.Id,
                Text = text
            };
        }

        private void SetupNxtAccount(NxtAccount nxtAccount, decimal balance)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.Is<string>(slackId => slackId == nxtAccount.SlackId))).ReturnsAsync(nxtAccount);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == nxtAccount))).ReturnsAsync(balance);
        }

        private void SetupCurrency(Currency c, decimal currencyBalance, string accountRs)
        {
            slackHandler.AddCurrency(c);
            nxtConnectorMock.Setup(connector => connector.GetCurrencyBalance(
                It.Is<ulong>(cId => cId == c.CurrencyId), 
                It.Is<string>(a => a == accountRs)))
                    .ReturnsAsync(currencyBalance);
        }
    }
}
