using System.Threading.Tasks;
using NxtLib;
using NxtLib.Accounts;
using NxtLib.Local;

namespace NxtTipBot
{
    public class NxtConnector
    {
        private readonly WalletDb wallet;
        private readonly IAccountService accountService;

        public NxtConnector(IServiceFactory serviceFactory, string walletfile)
        {
            wallet = InitWallet(walletfile);
            accountService = serviceFactory.CreateAccountService();
        }

        private WalletDb InitWallet(string walletfile)
        {
            var wallet = new WalletDb(walletfile);
            wallet.Init();
            return wallet;
        }

        public async Task<NxtAccount> GetAccount(string slackId)
        {
            return await wallet.GetAccount(slackId);
        }

        public async Task<NxtAccount> CreateAccount(string slackId)
        {
            var localPasswordGenerator = new LocalPasswordGenerator();
            var localAccountService = new LocalAccountService();

            var secretPhrase = localPasswordGenerator.GeneratePassword();
            var accountWithPublicKey = localAccountService.GetAccount(AccountIdLocator.BySecretPhrase(secretPhrase));

            var account = await wallet.CreateAccount(slackId, secretPhrase, accountWithPublicKey.AccountRs);
            return account;
        }

        public async Task<decimal> GetBalance(NxtAccount account)
        {
            var balanceReply = await accountService.GetBalance(account.NxtAccountId);
            return balanceReply.UnconfirmedBalance.Nxt;
        }
    }
}