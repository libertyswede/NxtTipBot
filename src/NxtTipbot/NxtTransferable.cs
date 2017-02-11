using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NxtTipbot
{

    public abstract class NxtTransferable
    {
        public string RecipientMessage { get; protected set; }
        public string ReactionId { get; set; }
        public ulong Id { get; }
        public string Name { get; }
        public int Decimals { get; }
        public abstract NxtTransferableType Type { get; }
        public ReadOnlyCollection<string> Monikers { get; }

        protected NxtTransferable (ulong id, string name, int decimals, List<string> monikers, string reactionId)
        {
            Monikers = monikers.AsReadOnly();
            Id = id;
            Name = name;
            Decimals = decimals;
            ReactionId = reactionId;
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
        private Nxt() : base(0, "NXT", 8, new List<string>(), "nxt")
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

        public NxtAsset(ulong assetId, string name, int decimals, string reactionId)
            : base(assetId, name, decimals, new List<string>(), reactionId)
        {
        }

        public NxtAsset (Asset asset, string recipientMessage, List<string> monikers, string reactionId) 
            : base(asset.AssetId, asset.Name, asset.Decimals, monikers, reactionId)
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

        public NxtCurrency(ulong currencyId, string currencyCode, int decimals, string reactionId)
            : base(currencyId, currencyCode, decimals, new List<string>(), reactionId)
        {
        }

        public NxtCurrency (Currency currency, string recipientMessage, List<string> monikers, string reactionId) 
            : base(currency.CurrencyId, currency.Code, currency.Decimals, monikers, reactionId)
        {
            RecipientMessage = recipientMessage;
        }

        public override bool HasRecipientMessage()
        {
            return !string.IsNullOrEmpty(RecipientMessage);
        }
    }
}