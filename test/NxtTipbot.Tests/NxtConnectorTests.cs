using Moq;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;
using System.Collections.Generic;
using Xunit;

namespace NxtTipbot.Tests
{
    public class NxtConnectorTests
    {
        private readonly Mock<IServiceFactory> serviceFactoryMock = new Mock<IServiceFactory>();
        private readonly Mock<IAccountService> accountServiceMock = new Mock<IAccountService>();
        private readonly Mock<IMonetarySystemService> monetarySystemServiceMock = new Mock<IMonetarySystemService>();
        private readonly Mock<IAssetExchangeService> assetExchangeServiceMock = new Mock<IAssetExchangeService>();

        private readonly NxtConnector nxtConnector;

        public NxtConnectorTests()
        {
            serviceFactoryMock.Setup(f => f.CreateAccountService()).Returns(accountServiceMock.Object);
            serviceFactoryMock.Setup(f => f.CreateMonetarySystemService()).Returns(monetarySystemServiceMock.Object);
            serviceFactoryMock.Setup(f => f.CreateAssetExchangeService()).Returns(assetExchangeServiceMock.Object);

            nxtConnector = new NxtConnector(serviceFactoryMock.Object);
        }

        [Fact]
        public async void TransferAssetShouldTransferCorrectAmount()
        {
            const decimal amount = 1.123M;
            var expectedQuantity = (long)(amount * 10000);
            assetExchangeServiceMock.Setup(s => s.TransferAsset(It.IsAny<Account>(), It.IsAny<ulong>(), It.IsAny<long>(), It.IsAny<CreateTransactionParameters>()))
                    .ReturnsAsync(new TransactionCreatedReply { TransactionId = 123 });

            await nxtConnector.Transfer(TestConstants.SenderAccount, TestConstants.RecipientAccount.NxtAccountRs, TestConstants.Asset, amount, "TEST");

            assetExchangeServiceMock.Verify(s => s.TransferAsset(
                It.IsAny<Account>(), 
                It.IsAny<ulong>(), 
                It.Is<long>(quantityQnt => quantityQnt == expectedQuantity),
                It.IsAny<CreateTransactionParameters>()));
        }

        [Theory]
        [InlineData(0, 100)]
        [InlineData(1, 10)]
        [InlineData(2, 1)]
        [InlineData(3, 0.1)]
        [InlineData(4, 0.01)]
        public async void GetBalanceShouldTransferCorrectAmount(int decimals, decimal expected)
        {
            var asset = new NxtAsset(new Asset { AssetId = 234, Decimals = decimals, Name = "TEST" }, "", new List<string>(), new List<TipReaction>());
            assetExchangeServiceMock.Setup(s => s.GetAccountAssets(
                It.Is<Account>(a => a.AccountRs == TestConstants.SenderAccount.NxtAccountRs),
                It.Is<ulong>(id => id == asset.Id),
                It.IsAny<bool?>(),
                It.IsAny<int?>(),
                It.IsAny<ulong?>(),
                It.IsAny<ulong?>()))
                    .ReturnsAsync(new AccountAssetsReply { AccountAssets = new List<AccountAsset> { new AccountAsset { UnconfirmedQuantityQnt = 100 } } });

            var balance = await nxtConnector.GetBalance(asset, TestConstants.SenderAccount.NxtAccountRs);

            Assert.Equal(expected, balance);
        }

        [Theory]
        [InlineData("NXT-7A48-47JL-T7LD-D5FS3")]
        [InlineData("NXT-5MYN-AP7M-NKMH-CRQJZ")]
        [InlineData("NXT-G885-AKDX-5G2B-BLUCG")]
        public void IsValidAddressRsShouldSucceed(string addressRs)
        {
            var isValid = nxtConnector.IsValidAddressRs(addressRs);

            Assert.True(isValid);
        }

        [Theory]
        [InlineData("NXT-7A48-47JL-T3LD-D5FS3")]
        [InlineData("NXT-5MYN-AP7M-NMMH-CRQJZ")]
        [InlineData("NXT-G885-AKDX-582B-BLUCG")]
        public void IsValidAddressRsShouldFail(string addressRs)
        {
            var isValid = nxtConnector.IsValidAddressRs(addressRs);

            Assert.False(isValid);
        }
    }
}