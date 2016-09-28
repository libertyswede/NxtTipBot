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
        NxtAccount CreateAccount(string slackId);
        Task<decimal> GetNxtBalance(NxtAccount account);
        Task<ulong> SendMoney(NxtAccount account, string addressRs, Amount amount, string message);
        Task<NxtCurrency> GetCurrency(ulong currencyId);
        Task<decimal> GetBalance(NxtTransferable transferable, string addressRs);
        Task<ulong> Transfer(NxtAccount account, string addressRs, NxtTransferable transferable, decimal amount, string message);
        Task<NxtAsset> GetAsset(ulong assetId, string name);
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

        public async Task<decimal> GetNxtBalance(NxtAccount account)
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
            
            return (decimal)unformattedBalance / (decimal)Math.Pow(10, Math.Max(transferable.Decimals, (byte)1));
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
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var units = (long)(amount * (long)Math.Pow(10, Math.Max(transferable.Decimals, (byte)1)));

            if (transferable.Type == NxtTransferableType.Currency)
            {
                return await TransferCurrency(addressRs, transferable.Id, units, parameters);
            }
            else if (transferable.Type == NxtTransferableType.Asset)
            {
                return await TransferAsset(addressRs, transferable.Id, units, parameters);
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