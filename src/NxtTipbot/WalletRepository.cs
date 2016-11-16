using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NxtTipbot.Model;
using System.Collections.Generic;

namespace NxtTipbot
{
    public interface IWalletRepository
    {
        Task<NxtAccount> GetAccount(string slackId);
        Task<NxtAccount> AddAccount(NxtAccount account);
        Task UpdateAccount(NxtAccount account);
    }

    public class WalletRepository : IWalletRepository
    {
        private readonly IBlockchainStore blockchainStore;
        private readonly bool doBlockchainBackup;

        public WalletRepository(IBlockchainStore blockchainStore)
        {
            this.blockchainStore = blockchainStore;
            doBlockchainBackup = blockchainStore != null;
        }

        public async Task<NxtAccount> GetAccount(string slackId)
        {
            using (var context = new WalletContext())
            {
                var account = await context.NxtAccounts.SingleOrDefaultAsync(a => a.SlackId == slackId);
                return account;
            }
        }

        public async Task<List<NxtAccount>> GetAllAccounts()
        {
            using (var context = new WalletContext())
            {
                var accounts = await context.NxtAccounts.ToListAsync();
                return accounts;
            }
        }

        public async Task<NxtAccount> AddAccount(NxtAccount account)
        {
            using (var context = new WalletContext())
            {
                context.NxtAccounts.Add(account);
                await context.SaveChangesAsync();
                if (doBlockchainBackup)
                {
                    await blockchainStore.BackupAccount(account);
                }
                return account;
            }
        }

        public async Task UpdateAccount(NxtAccount account)
        {
            using (var context = new WalletContext())
            {
                context.NxtAccounts.Attach(account);
                context.Entry(account).State = EntityState.Modified;
                await context.SaveChangesAsync();
            }
        }
    }
}