using System;
using System.Collections.Generic;
using Xunit;

namespace NxtTipbot.Tests
{
    public class TransferablesTests
    {
        private Transferables transferables = new Transferables();

        public TransferablesTests()
        {
        }

        [Fact]
        public void AddTransferableShouldNotAllowDuplicateId()
        {
            var asset1 = new NxtAsset(234, "asset1", 0);
            var asset2 = new NxtAsset(234, "asset2", 0);
            transferables.AddTransferable(asset1);

            Assert.Throws<ArgumentException>(() => transferables.AddTransferable(asset2));
        }

        [Fact]
        public void AddTransferableShouldNotAllowDuplicateNames()
        {
            var asset1 = new NxtAsset(123, "asset1", 0);
            var asset2 = new NxtAsset(234, "asset1", 0);
            transferables.AddTransferable(asset1);

            Assert.Throws<ArgumentException>(() => transferables.AddTransferable(asset2));
        }

        [Fact]
        public void AddTransferableShouldNotAllowDuplicateMonikerNames()
        {
            var asset1 = new NxtAsset(123, "asset1", 0);
            var asset2 = new NxtAsset(new NxtLib.AssetExchange.Asset { AssetId = 234, Decimals = 0, Name = "asset2" }, "", new List<string> { "asset1" });
            transferables.AddTransferable(asset1);

            Assert.Throws<ArgumentException>(() => transferables.AddTransferable(asset2));
        }

        [Fact]
        public void ContainsTransferableShouldReturnTrueForNxt()
        {
            Assert.True(transferables.ContainsTransferable(Nxt.Singleton));
        }

        [Fact]
        public void ContainsTransferableShouldReturnFalse()
        {
            Assert.False(transferables.ContainsTransferable(TestConstants.Asset));
        }

        [Fact]
        public void GetTransferableShouldReturnNull()
        {
            Assert.Null(transferables.GetTransferable("someunknownassetname"));
        }

        [Fact]
        public void GetTransferableShouldReturnValueByName()
        {
            const string assetname = "asset1";
            var expected = new NxtAsset(123, assetname, 0);
            GetTransferableShouldReturnExpectedValue(expected, assetname);
        }

        [Fact]
        public void GetTransferableShouldReturnValueById()
        {
            const ulong assetId = 123;
            var expected = new NxtAsset(assetId, "asset1", 0);
            GetTransferableShouldReturnExpectedValue(expected, assetId.ToString());
        }

        [Fact]
        public void GetTransferableShouldReturnValueByMoniker()
        {
            const string moniker = "assetmoniker";
            var expected = new NxtAsset(new NxtLib.AssetExchange.Asset { AssetId = 123, Decimals = 0, Name = "asset1" }, "", new List<string> { moniker });
            GetTransferableShouldReturnExpectedValue(expected, moniker);
        }

        private void GetTransferableShouldReturnExpectedValue(NxtAsset expected, string assetName)
        {
            transferables.AddTransferable(expected);

            var actual = transferables.GetTransferable(assetName);

            Assert.Same(expected, actual);
        }
    }
}
