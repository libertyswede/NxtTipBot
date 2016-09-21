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
        private readonly InstantMessage instantMessage = new InstantMessage { Id = "Id", UserId = "UserId" };
        private readonly User user = new User { Id = "UserId", Name = "XunitBot" };
        private readonly NxtAccount account = new NxtAccount
        {
            Id = 42,
            SlackId = "UserId",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-8MVA-XCVR-3JC9-2C7C3"
        };
        private readonly Currency currency = new Currency
        {
            CurrencyId = 123,
            Code = "TEST",
            Decimals = 4
        };
        private readonly string RecipientRs = "NXT-K5KL-23DJ-3XLK-22222";

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
            await slackHandler.InstantMessageRecieved(command, user, instantMessage);

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

            await slackHandler.InstantMessageRecieved(command, user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectBalance()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(account, expectedBalance);

            await slackHandler.InstantMessageRecieved("balance", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance))), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectCurrencyBalance()
        {
            const decimal expectedBalance = 42M;
            SetupNxtAccount(account, 1);
            SetupCurrency(currency, expectedBalance, account.NxtAccountRs);

            await slackHandler.InstantMessageRecieved("balance", user, instantMessage);

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
            nxtConnectorMock.Setup(c => c.CreateAccount(It.Is<string>(id => id == this.user.Id))).Returns(account);

            await slackHandler.InstantMessageRecieved(command, user, instantMessage);

            walletRepositoryMock.Verify(r => r.AddAccount(account));
            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.AccountCreated(account.NxtAccountRs))), true));
        }

        [Fact]
        public async void DepositShouldReturnAddress()
        {
            SetupNxtAccount(account, 1);

            await slackHandler.InstantMessageRecieved("deposit", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.DepositAddress(account.NxtAccountRs))), true));
        }

        [Theory]
        [InlineData("withdraw")]
        [InlineData(" WITHDRAW")]
        [InlineData("wItHdRaW")]
        public async void WithdrawShouldReturnNoAccount(string command)
        {
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(null);

            await slackHandler.InstantMessageRecieved($"{command} {RecipientRs} 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }

        [Fact]
        public async void WithdrawShouldReturnNotEnoughFunds()
        {
            const decimal balance = 4;
            SetupNxtAccount(account, balance);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawNxtShouldSucceed()
        {
            const decimal balance = 400;
            const decimal withdrawAmount = 42;
            const ulong txId = 928347;
            SetupNxtAccount(account, balance);
            nxtConnectorMock.Setup(c => c.SendMoney(
                It.Is<NxtAccount>(a => a == account), 
                It.Is<string>(r => r == RecipientRs), 
                It.Is<Amount>(a => a.Nxt == withdrawAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} {withdrawAmount}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, "NXT", txId))), true));
        }

        [Fact]
        public async void WithdrawShouldReturnUnknownUnit()
        {
            const string unknownUnit = "UNKNOWNS";
            SetupNxtAccount(account, 1);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} 42 {unknownUnit}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughNxtFunds()
        {
            const decimal balance = 0.9M;
            slackHandler.AddCurrency(currency);
            SetupNxtAccount(account, balance);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} 42 {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 1M;
            SetupNxtAccount(account, nxtBalance);
            SetupCurrency(currency, currencyBalance, account.NxtAccountRs);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} 42 {currency.Code}", user, instantMessage);

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
            SetupNxtAccount(account, nxtBalance);
            SetupCurrency(currency, currencyBalance, account.NxtAccountRs);
            nxtConnectorMock.Setup(c => c.TransferCurrency(
                It.Is<NxtAccount>(a => a == account), 
                It.Is<string>(r => r == RecipientRs),
                It.Is<ulong>(cId => cId == currency.CurrencyId),
                It.Is<long>(units => units == (long)withdrawAmount * Math.Pow(currency.Decimals, 10)), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageRecieved($"withdraw {RecipientRs} {withdrawAmount} {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, currency.Code, txId))), true));
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
