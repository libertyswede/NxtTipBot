using System;
using System.Linq;
using System.Threading.Tasks;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.AssetExchange;
using NxtLib.Local;
using NxtLib.MonetarySystem;
using NxtTipbot.Model;
using System.Collections.Generic;
using NxtLib.TaggedData;
using Newtonsoft.Json;

namespace NxtTipbot
{
    public interface INxtConnector
    {
        string MasterKey { set; }
        AccountWithPublicKey GetAccountFromSecretPhrase(string secretPhrase);
        Task<NxtAsset> GetAsset(TransferableConfig assetConfig);
        Task<NxtCurrency> GetCurrency(TransferableConfig currencyConfig);
        Task<decimal> GetBalance(NxtTransferable transferable, string addressRs);
        Task<Dictionary<NxtTransferable, decimal>> GetBalances(string addressRs, IList<NxtTransferable> existingTransferables);
        Task<ulong> Transfer(NxtAccount senderAccount, string addressRs, NxtTransferable transferable, decimal amount, string message, string recipientPublicKey = "");
        string GenerateMasterKey();
        void SetNxtProperties(NxtAccount account);
        bool IsValidAddressRs(string addressRs);
        Task<List<Model.EncryptedMessage>> SearchTaggedData(string accountRs, string tags);
        string Decrypt(Model.EncryptedMessage message, AccountWithPublicKey account, string secretPhrase);
        Model.EncryptedMessage Encrypt(string data, AccountWithPublicKey account, string secretPhrase);
        Task UploadTaggedData(string data, string name, string tags, string description, string secretPhrase);
    }

    public class NxtConnector : INxtConnector
    {
        private readonly IAccountService accountService;
        private readonly IMonetarySystemService monetarySystemService;
        private readonly IAssetExchangeService assetExchangeService;
        private readonly ITaggedDataService taggedDataService;

        public string MasterKey { private get; set; }

        public NxtConnector(IServiceFactory serviceFactory)
        {
            accountService = serviceFactory.CreateAccountService();
            monetarySystemService = serviceFactory.CreateMonetarySystemService();
            assetExchangeService = serviceFactory.CreateAssetExchangeService();
            taggedDataService = serviceFactory.CreateTaggedDataService();
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
            var accountWithPublicKey = GetAccountFromSecretPhrase(account.SecretPhrase);
            account.NxtPublicKey = accountWithPublicKey.PublicKey.ToHexString();
            account.NxtAccountRs = accountWithPublicKey.AccountRs;
        }

        public AccountWithPublicKey GetAccountFromSecretPhrase(string secretPhrase)
        {
            var localAccountService = new LocalAccountService();
            var accountWithPublicKey = localAccountService.GetAccount(AccountIdLocator.BySecretPhrase(secretPhrase));
            return accountWithPublicKey;
        }

        public async Task<NxtAsset> GetAsset(TransferableConfig assetConfig)
        {
            var asset = await assetExchangeService.GetAsset(assetConfig.Id);
            return new NxtAsset(asset, assetConfig.RecipientMessage, assetConfig.Monikers, assetConfig.ReactionId);
        }

        public async Task<NxtCurrency> GetCurrency(TransferableConfig currencyConfig)
        {
            var currencyReply = await monetarySystemService.GetCurrency(CurrencyLocator.ByCurrencyId(currencyConfig.Id));
            return new NxtCurrency(currencyReply, currencyConfig.RecipientMessage, currencyConfig.Monikers, currencyConfig.ReactionId);
        }

        public async Task<Dictionary<NxtTransferable, decimal>> GetBalances(string addressRs, IList<NxtTransferable> existingTransferables)
        {
            var nxtTask = GetNxtBalance(addressRs);
            var assetsTask = GetAssets(addressRs);
            var currenciesTask = GetCurrencies(addressRs);

            await Task.WhenAll(nxtTask, assetsTask, currenciesTask);

            var result = new Dictionary<NxtTransferable, decimal>();
            result.Add(Nxt.Singleton, nxtTask.Result / (decimal)Math.Pow(10, Nxt.Singleton.Decimals));

            foreach (var accountAsset in assetsTask.Result)
            {
                var transferable = existingTransferables.SingleOrDefault(t => t.Id == accountAsset.AssetId) ??
                    new NxtAsset(accountAsset.AssetId, accountAsset.Name, accountAsset.Decimals, "");

                result.Add(transferable, accountAsset.QuantityQnt / (decimal)Math.Pow(10, accountAsset.Decimals));
            }

            foreach (var accountCurrency in currenciesTask.Result)
            {
                var transferable = existingTransferables.SingleOrDefault(t => t.Id == accountCurrency.CurrencyId) ??
                    new NxtCurrency(accountCurrency.CurrencyId, accountCurrency.Code, accountCurrency.Decimals, "");

                result.Add(transferable, accountCurrency.UnconfirmedUnits / (decimal)Math.Pow(10, accountCurrency.Decimals));
            }
            return result;
        }

        private async Task<List<AccountAsset>> GetAssets(string addressRs)
        {
            var accountAssetReply = await assetExchangeService.GetAccountAssets(addressRs, includeAssetInfo: true);
            return accountAssetReply.AccountAssets;
        }

        private async Task<List<AccountCurrency>> GetCurrencies(string addressRs)
        {
            var accountCurrencyReply = await monetarySystemService.GetAccountCurrencies(addressRs, includeCurrencyInfo: true);
            return accountCurrencyReply.AccountCurrencies;
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
                case NxtTransferableType.Nxt: return await SendMoney(parameters, addressRs, quantity);
                case NxtTransferableType.Asset: return await TransferAsset(addressRs, transferable.Id, quantity, parameters);
                case NxtTransferableType.Currency: return await TransferCurrency(addressRs, transferable.Id, quantity, parameters);
                default: throw new ArgumentException($"Unsupported NxtTransferableType: {transferable.Type}", nameof(transferable));
            }
        }

        private async Task<ulong> SendMoney(CreateTransactionParameters parameters, string addressRs, long amountNqt)
        {
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

        public async Task<List<Model.EncryptedMessage>> SearchTaggedData(string accountRs, string tags)
        {
            var reply = await taggedDataService.SearchTaggedData(null, tags, accountRs, includeData: true);
            var list = reply.Data.Select(d => JsonConvert.DeserializeObject<Model.EncryptedMessage>(d.Data)).ToList();
            return list;
        }

        public string Decrypt(Model.EncryptedMessage message, AccountWithPublicKey account, string secretPhrase)
        {
            var localMessageService = new LocalMessageService();
            var decrypted = localMessageService.DecryptTextFrom(account.PublicKey, message.Message, message.Nonce, true, secretPhrase);
            return decrypted;
        }

        public Model.EncryptedMessage Encrypt(string data, AccountWithPublicKey account, string secretPhrase)
        {
            var localMessageService = new LocalMessageService();
            var nonce = localMessageService.CreateNonce();
            var encrypted = localMessageService.EncryptTextTo(account.PublicKey, data, nonce, true, secretPhrase);
            return new Model.EncryptedMessage
            {
                Message = encrypted.ToString(),
                Nonce = nonce.ToString()
            };
        }

        public async Task UploadTaggedData(string data, string name, string tags, string description, string secretPhrase)
        {
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, secretPhrase);
            var reply = await taggedDataService.UploadTaggedData(name, data, parameters, null, description, tags, isText: true);
        }
    }
}