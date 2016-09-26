using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;

namespace NxtTipbot
{

    public abstract class NxtTransferable
    {
        public ulong Id { get; }
        public string Name { get; }
        public int Decimals { get; }
        public abstract NxtTransferableType Type { get; }

        protected NxtTransferable (ulong id, string name, int decimals)
        {
            Id = id;
            Name = name;
            Decimals = decimals;
        }
    }

    public enum NxtTransferableType
    {
        Asset,
        Currency
    }

    public class NxtAsset : NxtTransferable
    {
        public override NxtTransferableType Type { get { return NxtTransferableType.Asset; } }
        public NxtAsset (Asset asset, string name) : base(asset.AssetId, name, asset.Decimals)
        {
        }
    }

    public class NxtCurrency : NxtTransferable
    {
        public override NxtTransferableType Type { get { return NxtTransferableType.Currency; } }
        public NxtCurrency (Currency currency) : base(currency.CurrencyId, currency.Code, currency.Decimals)
        {
        }
    }
}