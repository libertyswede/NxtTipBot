using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

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
                It.Is<string>(input => input.StartsWith("*Direct Message Commands*")), true));
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
                It.Is<string>(input => input.StartsWith("You do currently not have an account")), true));
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
                It.Is<string>(input => input.StartsWith($"Your current balance is {expectedBalance} NXT")), true));
        }
    }
}
