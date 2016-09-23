using System;
using System.Threading.Tasks;
using NxtLib;
using NxtLib.Accounts;
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
        Task<decimal> GetCurrencyBalance(ulong currencyId, string addressRs);
        Task<ulong> TransferCurrency(NxtAccount account, string addressRs, Currency currency, decimal amount, string message);
    }

    public class NxtConnector : INxtConnector
    {
        private readonly IAccountService accountService;
        private readonly IMonetarySystemService monetarySystemService;

        public NxtConnector(IServiceFactory serviceFactory)
        {
            accountService = serviceFactory.CreateAccountService();
            monetarySystemService = serviceFactory.CreateMonetarySystemService();
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

        public async Task<decimal> GetCurrencyBalance(ulong currencyId, string addressRs)
        {
            var accountCurrencyReply = await monetarySystemService.GetAccountCurrencies(addressRs, currencyId, includeCurrencyInfo: true);
            return (decimal)accountCurrencyReply.UnconfirmedUnits / Math.Max(accountCurrencyReply.Decimals, (byte)1);
        }

        public async Task<ulong> TransferCurrency(NxtAccount account, string addressRs, Currency currency, decimal amount, string message)
        {
            var parameters = new CreateTransactionBySecretPhrase(true, 1440, Amount.OneNxt, account.SecretPhrase);
            parameters.Message = new CreateTransactionParameters.UnencryptedMessage(message, true);
            var units = (long)(amount * (long)Math.Pow(Math.Max(currency.Decimals, (byte)1), 10));
            var transferCurrencyReply = await monetarySystemService.TransferCurrency(addressRs, currency.CurrencyId, units, parameters);

            return transferCurrencyReply.TransactionId.Value;
        }
    }
}