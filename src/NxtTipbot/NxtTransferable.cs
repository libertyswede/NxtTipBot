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
        Nxt,
        Asset,
        Currency
    }

    public class Nxt : NxtTransferable
    {
        public static readonly Nxt Singleton = new Nxt();
        public override NxtTransferableType Type { get { return NxtTransferableType.Nxt; } }
        private Nxt() : base(0, "NXT", 8)
        {
        }
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