using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NxtTipbot
{

    public abstract class NxtTransferable
    {
        public string RecipientMessage { get; protected set; }
        public ulong Id { get; }
        public string Name { get; }
        public int Decimals { get; }
        public abstract NxtTransferableType Type { get; }
        public ReadOnlyCollection<string> Monikers { get; }

        protected NxtTransferable (ulong id, string name, int decimals, List<string> monikers)
        {
            Monikers = monikers.AsReadOnly();
            Id = id;
            Name = name;
            Decimals = decimals;
        }

        public abstract bool HasRecipientMessage();
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
        private Nxt() : base(0, "NXT", 8, new List<string>())
        {
        }

        public override bool HasRecipientMessage()
        {
            return false;
        }
    }

    public class NxtAsset : NxtTransferable
    {
        public override NxtTransferableType Type { get { return NxtTransferableType.Asset; } }
        public NxtAsset (Asset asset, string name, string recipientMessage, List<string> monikers) 
            : base(asset.AssetId, name, asset.Decimals, monikers)
        {
            RecipientMessage = recipientMessage;
        }

        public override bool HasRecipientMessage()
        {
            return !string.IsNullOrEmpty(RecipientMessage);
        }
    }

    public class NxtCurrency : NxtTransferable
    {
        public override NxtTransferableType Type { get { return NxtTransferableType.Currency; } }
        public NxtCurrency (Currency currency, string recipientMessage, List<string> monikers) 
            : base(currency.CurrencyId, currency.Code, currency.Decimals, monikers)
        {
            RecipientMessage = recipientMessage;
        }

        public override bool HasRecipientMessage()
        {
            return !string.IsNullOrEmpty(RecipientMessage);
        }
    }
}