using NxtLib.AssetExchange;
using NxtLib.MonetarySystem;
using NxtTipbot.Model;
using System.Collections.Generic;

namespace NxtTipbot.Tests
{
    public static class TestConstants
    {
        public static readonly NxtAccount SenderAccount = new NxtAccount
        {
            Id = 42,
            SlackId = "SlackUserId",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-8MVA-XCVR-3JC9-2C7C3"
        };

        public static readonly NxtAccount RecipientAccount = new NxtAccount
        {
            Id = 43,
            SlackId = "RecipientUserId",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-K5KL-23DJ-3XLK-22222"
        };

        public static readonly NxtAccount RecipientAccount2 = new NxtAccount
        {
            Id = 44,
            SlackId = "RecipientUserId2",
            SecretPhrase = "TopSecret",
            NxtAccountRs = "NXT-K5KL-23DJ-3XLK-22223"
        };

        public static readonly NxtCurrency Currency = new NxtCurrency(new Currency
        {
            CurrencyId = 123,
            Code = "TEST",
            Decimals = 4
        }, "{sender} just sent you {amount} TEST!", new List<string>(), new List<TipReaction>());

        public static readonly NxtAsset Asset = new NxtAsset(new Asset
        {
            AssetId = 234,
            Decimals = 4,
            Name = "TEST"
        }, "{sender} just sent you {amount} TEST!", new List<string> { "TST", "TESTT" }, new List<TipReaction>());

        public static readonly string ValidAddressRs1 = "NXT-G885-AKDX-5G2B-BLUCG";
        public static readonly string InvalidAddressRs1 = "NXT-G885-AKDX-582B-BLUCG";
        
    }
}