using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

namespace NxtTipbot.Tests
{
    public class SlackHandlerTests
    {
        [Theory]
        [InlineData("help")]
        [InlineData(" HELP ")]
        [InlineData("hElP ")]
        public async void SendHelpText(string command)
        {
            var nxtConnectorMock = new Mock<INxtConnector>();
            var walletRepositoryMock = new Mock<IWalletRepository>();
            var loggerMock = new Mock<ILogger>();
            var slackConnectorMock = new Mock<ISlackConnector>();
            var slackHandler = new SlackHandler(nxtConnectorMock.Object, walletRepositoryMock.Object, loggerMock.Object);
            slackHandler.SlackConnector = slackConnectorMock.Object;
            var instantMessage = new InstantMessage { Id = "Id", UserId = "UserId" };

            await slackHandler.InstantMessageRecieved(command, null, instantMessage);

            slackConnectorMock.Verify(c => c.SendMessage(instantMessage.Id, 
                It.Is<string>(input => input.StartsWith("*Direct Message Commands*")), true));
        }
    }
}
