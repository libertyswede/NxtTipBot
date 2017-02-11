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
        Task<bool> GetUserReactionTipSetting(string slackId);
        Task SetUserReactionTipSetting(string slackId, bool value);
    }

    public class WalletRepository : IWalletRepository
    {
        private readonly IBlockchainStore blockchainStore;
        private readonly bool doBlockchainBackup;
        private const string reactionTipSettingKey = "reaction_tip";

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

        public async Task<bool> GetUserReactionTipSetting(string slackId)
        {
            using (var context = new WalletContext())
            {
                var setting = await context.UserSettings.SingleOrDefaultAsync(u => u.Account.SlackId == slackId && u.Key == reactionTipSettingKey);
                if (setting == null)
                {
                    return false;
                }
                return setting.Value.Equals(true.ToString());
            }
        }

        public async Task SetUserReactionTipSetting(string slackId, bool value)
        {
            using (var context = new WalletContext())
            {
                var setting = await context.UserSettings.SingleOrDefaultAsync(u => u.Account.SlackId == slackId && u.Key == reactionTipSettingKey);
                if (setting != null)
                {
                    setting.Value = value.ToString();
                    context.Entry(setting).State = EntityState.Modified;
                }
                else
                {
                    var account = await GetAccount(slackId);
                    setting = new UserSetting { AccountId = account.Id, Key = reactionTipSettingKey, Value = value.ToString() };
                    context.Add(setting);
                }
                await context.SaveChangesAsync();
            }
        }
    }
}