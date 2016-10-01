using System;
using System.Linq;
using System.Threading.Tasks;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.AssetExchange;
using NxtLib.Local;
using NxtLib.MonetarySystem;
using NxtTipbot.Model;

namespace NxtTipbot
{
    public interface INxtConnector
    {
        string MasterKey { set; }
        Task<decimal> GetNxtBalance(NxtAccount account);
        Task<ulong> SendMoney(NxtAccount account, string addressRs, Amount amount, string message);
        Task<NxtCurrency> GetCurrency(ulong currencyId);
        Task<decimal> GetBalance(NxtTransferable transferable, string addressRs);
        Task<ulong> Transfer(NxtAccount account, string addressRs, NxtTransferable transferable, decimal amount, string message);
        Task<NxtAsset> GetAsset(ulong assetId, string name);
        string GenerateMasterKey();
        void SetNxtProperties(NxtAccount account);
    }

    public class NxtConnector : INxtConnector
    {
        private readonly IAccountService accountService;
        private readonly IMonetarySystemService monetarySystemService;
        private readonly IAssetExchangeService assetExchangeService;

        public string MasterKey { private get; set; }

        public NxtConnector(IServiceFactory serviceFactory)
        {
            accountService = serviceFactory.CreateAccountService();
            monetarySystemService = serviceFactory.CreateMonetarySystemService();
            assetExchangeService = serviceFactory.CreateAssetExchangeService();
        }

        public string GenerateMasterKey()
        {
            var localPasswordGenerator = new LocalPasswordGenerator();
            return localPasswordGenerator.GeneratePassword(256);
        }

        public void SetNxtProperties(NxtAccount account)
        {
            if (string.IsNullOrEmpty(account.SecretPhrase))
            {
                SetSecretPhrase(account);
                SetAccountRs(account);
            }
            else if (string.IsNullOrEmpty(account.NxtAccountRs))
            {
                SetAccountRs(account);
            }
        }

        private void SetSecretPhrase(NxtAccount account)
        {
            var localPasswordGenerator = new LocalPasswordGenerator();
            var secretPhrase = localPasswordGenerator.GenerateDetermenisticPassword(MasterKey, account.Id, 256);
            account.SecretPhrase = secretPhrase;
        }

        private void SetAccountRs(NxtAccount account)
        {
            var localAccountService = new LocalAccountService();
            var accountWithPublicKey = localAccountService.GetAccount(AccountIdLocator.BySecretPhrase(account.SecretPhrase));
            account.NxtAccountRs = accountWithPublicKey.AccountRs;
        }

        public async Task<decimal> GetNxtBalance(NxtAccount account)
        {
            var balanceReply = await accountService.GetBalance(account.NxtAccountRs);
            return balanceReply.UnconfirmedBalance.Nxt;
        }

        public async Task<ulong> SendMoney(NxtAccount account, string addressRs, Amount amount, string message)
        {
            SetNxtProperties(account);
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true); 
            var sendMoneyReply = await accountService.SendMoney(parameters, addressRs, amount);
            
            return sendMoneyReply.TransactionId.Value;
        }

        public async Task<NxtCurrency> GetCurrency(ulong currencyId)
        {
            var currencyReply = await monetarySystemService.GetCurrency(CurrencyLocator.ByCurrencyId(currencyId));
            return new NxtCurrency(currencyReply);
        }
        
        public async Task<decimal> GetBalance(NxtTransferable transferable, string addressRs)
        {
            var unformattedBalance = (transferable.Type == NxtTransferableType.Currency) ? 
                await GetCurrencyBalance(transferable.Id, addressRs) : 
                await GetAssetBalance(transferable.Id, addressRs);
            
            return unformattedBalance / (decimal)Math.Pow(10, Math.Max(transferable.Decimals, 1));
        }

        private async Task<long> GetCurrencyBalance(ulong currencyId, string addressRs)
        {
            var accountCurrencyReply = await monetarySystemService.GetAccountCurrencies(addressRs, currencyId);
            return accountCurrencyReply.AccountCurrencies.SingleOrDefault()?.UnconfirmedUnits ?? 0;
        }

        private async Task<long> GetAssetBalance(ulong assetId, string addressRs)
        {
            var accountAssetsReply = await assetExchangeService.GetAccountAssets(addressRs, assetId);
            return accountAssetsReply.AccountAssets.SingleOrDefault()?.UnconfirmedQuantityQnt ?? 0;
        }

        public async Task<ulong> Transfer(NxtAccount account, string addressRs, NxtTransferable transferable, decimal amount, string message)
        {
            SetNxtProperties(account);
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var quantity = (long)(amount * (long)Math.Pow(10, Math.Max(transferable.Decimals, 1)));

            if (transferable.Type == NxtTransferableType.Currency)
            {
                return await TransferCurrency(addressRs, transferable.Id, quantity, parameters);
            }
            else if (transferable.Type == NxtTransferableType.Asset)
            {
                return await TransferAsset(addressRs, transferable.Id, quantity, parameters);
            }
            throw new ArgumentException($"Unsupported NxtTransferableType: {transferable.Type}", nameof(transferable));
        }

        private async Task<ulong> TransferAsset(string addressRs, ulong assetId, long quantityQnt, CreateTransactionParameters parameters)
        {
            var transferAssetReply = await assetExchangeService.TransferAsset(addressRs, assetId, quantityQnt, parameters);
            return transferAssetReply.TransactionId.Value;
        }

        private async Task<ulong> TransferCurrency(string addressRs, ulong currencyId, long units, CreateTransactionParameters parameters)
        {
            var transferCurrencyReply = await monetarySystemService.TransferCurrency(addressRs, currencyId, units, parameters);
            return transferCurrencyReply.TransactionId.Value;
        }

        public async Task<NxtAsset> GetAsset(ulong assetId, string name)
        {
            var asset = await assetExchangeService.GetAsset(assetId);
            return new NxtAsset(asset, name);
        }
    }
}