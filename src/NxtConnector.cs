using NxtLib;

namespace NxtTipBot
{
    public class NxtConnector
    {
        private readonly WalletDb wallet;

        public NxtConnector(IServiceFactory serviceFactory, string walletfile)
        {
            wallet = InitWallet(walletfile);
        }

        private WalletDb InitWallet(string walletfile)
        {
            var wallet = new WalletDb(walletfile);
            wallet.Init();
            return wallet;
        }
    }
}