using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using NxtLib.MonetarySystem;
using NxtLib;

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
            var account = new NxtAccount();
            const decimal expectedBalance = 42M;
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(r => r.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(expectedBalance);

            await slackHandler.InstantMessageRecieved("balance", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.CurrentBalance(expectedBalance))), true));
        }
        
        [Fact]
        public async void BalanceShouldReturnCorrectCurrencyBalance()
        {
            var account = new NxtAccount();
            const decimal expectedBalance = 42M;
            slackHandler.AddCurrency(currency);
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(r => r.GetCurrencyBalance(It.IsAny<ulong>(), It.IsAny<string>())).ReturnsAsync(expectedBalance);

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
            var account = new NxtAccount{SlackId = this.user.Id, NxtAccountRs = "NXT-123"};
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
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);

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

            await slackHandler.InstantMessageRecieved($"{command} NXT-123 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NoAccount)), true));
        }

        [Fact]
        public async void WithdrawShouldReturnNotEnoughFunds()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const decimal balance = 4;
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(balance);

            await slackHandler.InstantMessageRecieved("withdraw NXT-123 42", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawNxtShouldSucceed()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const string recipient = "NXT-123";
            const decimal balance = 400;
            const decimal withdrawAmount = 42;
            const ulong txId = 928347; // true random number...
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(balance);
            nxtConnectorMock.Setup(c => c.SendMoney(
                It.Is<NxtAccount>(a => a == account), 
                It.Is<string>(r => r == recipient), 
                It.Is<Amount>(a => a.Nxt == withdrawAmount), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageRecieved($"withdraw {recipient} {withdrawAmount}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, "NXT", txId))), true));
        }

        [Fact]
        public async void WithdrawShouldReturnUnknownUnit()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const string unknownUnit = "UNKNOWNS";
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);

            await slackHandler.InstantMessageRecieved($"withdraw NXT-123 42 {unknownUnit}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.UnknownUnit(unknownUnit))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughNxtFunds()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const decimal balance = 0.9M;
            slackHandler.AddCurrency(currency);
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(balance);

            await slackHandler.InstantMessageRecieved($"withdraw NXT-123 42 {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(balance, "NXT"))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldReturnNotEnoughCurrencyFunds()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 1M;
            slackHandler.AddCurrency(currency);
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(nxtBalance);
            nxtConnectorMock.Setup(c => c.GetCurrencyBalance(It.Is<ulong>(cId => cId == currency.CurrencyId), It.Is<string>(a => a == account.NxtAccountRs)))
                .ReturnsAsync(currencyBalance);

            await slackHandler.InstantMessageRecieved($"withdraw NXT-123 42 {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.NotEnoughFunds(currencyBalance, currency.Code))), true));
        }

        [Fact]
        public async void WithdrawCurrencyShouldSucceed()
        {
            var account = new NxtAccount{NxtAccountRs = "NXT-123"};
            const decimal nxtBalance = 1M;
            const decimal currencyBalance = 100M;
            const decimal withdrawAmount = 42;
            const string recipient = "NXT-123";
            const ulong txId = 9837425;
            slackHandler.AddCurrency(currency);
            walletRepositoryMock.Setup(r => r.GetAccount(It.IsAny<string>())).ReturnsAsync(account);
            nxtConnectorMock.Setup(c => c.GetBalance(It.Is<NxtAccount>(a => a == account))).ReturnsAsync(nxtBalance);
            nxtConnectorMock.Setup(c => c.GetCurrencyBalance(It.Is<ulong>(cId => cId == currency.CurrencyId), It.Is<string>(a => a == account.NxtAccountRs)))
                .ReturnsAsync(currencyBalance);
            nxtConnectorMock.Setup(c => c.TransferCurrency(
                It.Is<NxtAccount>(a => a == account), 
                It.Is<string>(r => r == recipient),
                It.Is<ulong>(cId => cId == currency.CurrencyId),
                It.IsAny<long>(), 
                It.IsAny<string>()))
                    .ReturnsAsync(txId);

            await slackHandler.InstantMessageRecieved($"withdraw NXT-123 {withdrawAmount} {currency.Code}", user, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.Equals(MessageConstants.Withdraw(withdrawAmount, currency.Code, txId))), true));
        }
    }
}
