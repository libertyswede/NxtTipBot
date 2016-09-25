using System;
using System.Linq;
using System.Threading.Tasks;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.AssetExchange;
using NxtLib.Local;
using NxtLib.MonetarySystem;

namespace NxtTipbot
{
    public interface INxtConnector
    {
        NxtAccount CreateAccount(string slackId);
        Task<decimal> GetBalance(NxtAccount account);
        Task<ulong> SendMoney(NxtAccount account, string addressRs, Amount amount, string message);
        Task<Currency> GetCurrency(ulong currencyId);
        Task<decimal> GetCurrencyBalance(Currency currency, string addressRs);
        Task<ulong> TransferCurrency(NxtAccount account, string addressRs, Currency currency, decimal amount, string message);
        Task<Asset> GetAsset(ulong assetId);
        Task<decimal> GetAssetBalance(Asset asset, string addressRs);
    }

    public class NxtConnector : INxtConnector
    {
        private readonly IAccountService accountService;
        private readonly IMonetarySystemService monetarySystemService;
        private readonly IAssetExchangeService assetExchangeService;

        public NxtConnector(IServiceFactory serviceFactory)
        {
            accountService = serviceFactory.CreateAccountService();
            monetarySystemService = serviceFactory.CreateMonetarySystemService();
            assetExchangeService = serviceFactory.CreateAssetExchangeService();
        }

        public NxtAccount CreateAccount(string slackId)
        {
            var localPasswordGenerator = new LocalPasswordGenerator();
            var localAccountService = new LocalAccountService();

            var secretPhrase = localPasswordGenerator.GeneratePassword();
            var accountWithPublicKey = localAccountService.GetAccount(AccountIdLocator.BySecretPhrase(secretPhrase));

            var account = new NxtAccount { SlackId = slackId, SecretPhrase = secretPhrase, NxtAccountRs = accountWithPublicKey.AccountRs };
            return account;
        }

        public async Task<decimal> GetBalance(NxtAccount account)
        {
            var balanceReply = await accountService.GetBalance(account.NxtAccountRs);
            return balanceReply.UnconfirmedBalance.Nxt;
        }

        public async Task<ulong> SendMoney(NxtAccount account, string addressRs, Amount amount, string message)
        {
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true); 
            var sendMoneyReply = await accountService.SendMoney(parameters, addressRs, amount);
            
            return sendMoneyReply.TransactionId.Value;
        }

        public async Task<Currency> GetCurrency(ulong currencyId)
        {
            var currencyReply = await monetarySystemService.GetCurrency(CurrencyLocator.ByCurrencyId(currencyId));
            return currencyReply;
        }

        public async Task<decimal> GetCurrencyBalance(Currency currency, string addressRs)
        {
            var accountCurrencyReply = await monetarySystemService.GetAccountCurrencies(addressRs, currency.CurrencyId);
            return (decimal)accountCurrencyReply.UnconfirmedUnits / (decimal)Math.Pow(10, Math.Max(currency.Decimals, (byte)1));
        }

        public async Task<ulong> TransferCurrency(NxtAccount account, string addressRs, Currency currency, decimal amount, string message)
        {
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var units = (long)(amount * (long)Math.Pow(Math.Max(currency.Decimals, (byte)1), 10));
            var transferCurrencyReply = await monetarySystemService.TransferCurrency(addressRs, currency.CurrencyId, units, parameters);

            return transferCurrencyReply.TransactionId.Value;
        }

        public async Task<Asset> GetAsset(ulong assetId)
        {
            var asset = await assetExchangeService.GetAsset(assetId);
            return asset;
        }

        public async Task<decimal> GetAssetBalance(Asset asset, string addressRs)
        {
            var accountAssetsReply = await assetExchangeService.GetAccountAssets(addressRs, asset.AssetId);
            var accountAsset = accountAssetsReply.AccountAssets.ToList().SingleOrDefault();
            if (accountAsset == null)
            {
                return 0M;
            }
            return (decimal)accountAsset.UnconfirmedQuantityQnt / (decimal)Math.Pow(10, Math.Max(asset.Decimals, (byte)1));
        }
    }
}