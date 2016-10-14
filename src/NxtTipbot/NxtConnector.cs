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
        Task<NxtAsset> GetAsset(AssetConfig assetConfig);
        Task<NxtCurrency> GetCurrency(ulong currencyId);
        Task<decimal> GetBalance(NxtTransferable transferable, string addressRs);
        Task<ulong> Transfer(NxtAccount senderAccount, string addressRs, NxtTransferable transferable, decimal amount, string message, string recipientPublicKey = "");
        string GenerateMasterKey();
        void SetNxtProperties(NxtAccount account);
        bool IsValidAddressRs(string addressRs);
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
            account.NxtPublicKey = accountWithPublicKey.PublicKey.ToHexString();
            account.NxtAccountRs = accountWithPublicKey.AccountRs;
        }

        public async Task<NxtAsset> GetAsset(AssetConfig assetConfig)
        {
            var asset = await assetExchangeService.GetAsset(assetConfig.Id);
            return new NxtAsset(asset, assetConfig.Name, assetConfig.RecipientMessage);
        }

        public async Task<NxtCurrency> GetCurrency(ulong currencyId)
        {
            var currencyReply = await monetarySystemService.GetCurrency(CurrencyLocator.ByCurrencyId(currencyId));
            return new NxtCurrency(currencyReply);
        }
        
        public async Task<decimal> GetBalance(NxtTransferable transferable, string addressRs)
        {
            decimal unformattedBalance;
            switch (transferable.Type)
            {
                case NxtTransferableType.Nxt: unformattedBalance = await GetNxtBalance(addressRs);
                    break;
                case NxtTransferableType.Asset: unformattedBalance = await GetAssetBalance(transferable.Id, addressRs);
                    break;
                case NxtTransferableType.Currency: unformattedBalance = await GetCurrencyBalance(transferable.Id, addressRs);
                    break;
                default: throw new ArgumentException($"Unsupported NxtTransferableType: {transferable.Type}", nameof(transferable));
            }
            
            return unformattedBalance / (decimal)Math.Pow(10, transferable.Decimals);
        }

        private async Task<decimal> GetNxtBalance(string addressRs)
        {
            var balanceReply = await accountService.GetBalance(addressRs);
            return balanceReply.UnconfirmedBalance.Nqt;
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

        public async Task<ulong> Transfer(NxtAccount senderAccount, string addressRs, NxtTransferable transferable, decimal amount, string message, string recipientPublicKey = "")
        {
            SetNxtProperties(senderAccount);
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, senderAccount.SecretPhrase);
            parameters.RecipientPublicKey = recipientPublicKey;
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var quantity = (long)(amount * (long)Math.Pow(10, transferable.Decimals));

            switch (transferable.Type)
            {
                case NxtTransferableType.Nxt: return await SendMoney(senderAccount, addressRs, quantity, message, recipientPublicKey);
                case NxtTransferableType.Asset: return await TransferAsset(addressRs, transferable.Id, quantity, parameters);
                case NxtTransferableType.Currency: return await TransferCurrency(addressRs, transferable.Id, quantity, parameters);
                default: throw new ArgumentException($"Unsupported NxtTransferableType: {transferable.Type}", nameof(transferable));
            }
        }

        private async Task<ulong> SendMoney(NxtAccount senderAccount, string addressRs, long amountNqt, string message, string recipientPublicKey = "")
        {
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, senderAccount.SecretPhrase);
            parameters.RecipientPublicKey = recipientPublicKey;
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var sendMoneyReply = await accountService.SendMoney(parameters, addressRs, Amount.CreateAmountFromNqt(amountNqt));

            return sendMoneyReply.TransactionId.Value;
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

        public bool IsValidAddressRs(string addressRs)
        {
            try
            {
                var account = new Account(addressRs);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}