using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NxtTipbot
{

    public abstract class NxtTransferable
    {
        public string RecipientMessage { get; protected set; }
        public List<TipReaction> Reactions { get; }
        public ulong Id { get; }
        public string Name { get; }
        public int Decimals { get; }
        public abstract NxtTransferableType Type { get; }
        public ReadOnlyCollection<string> Monikers { get; }

        protected NxtTransferable (ulong id, string name, int decimals, List<string> monikers, List<TipReaction> reactions)
        {
            Monikers = monikers.AsReadOnly();
            Id = id;
            Name = name;
            Decimals = decimals;
            Reactions = reactions;
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
        private Nxt() : base(0, "NXT", 8, new List<string>(), new List<TipReaction> { new TipReaction("nxt", "You got awarded with the NXT emoji!", 5M) })
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

        public NxtAsset(ulong assetId, string name, int decimals, List<TipReaction> reactions)
            : base(assetId, name, decimals, new List<string>(), reactions)
        {
        }

        public NxtAsset (Asset asset, string recipientMessage, List<string> monikers, List<TipReaction> reactions) 
            : base(asset.AssetId, asset.Name, asset.Decimals, monikers, reactions)
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

        public NxtCurrency(ulong currencyId, string currencyCode, int decimals, List<TipReaction> reactions)
            : base(currencyId, currencyCode, decimals, new List<string>(), reactions)
        {
        }

        public NxtCurrency (Currency currency, string recipientMessage, List<string> monikers, List<TipReaction> reactions) 
            : base(currency.CurrencyId, currency.Code, currency.Decimals, monikers, reactions)
        {
            RecipientMessage = recipientMessage;
        }

        public override bool HasRecipientMessage()
        {
            return !string.IsNullOrEmpty(RecipientMessage);
        }
    }
}