using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NxtTipbot.Model;

namespace NxtTipbot
{
    public interface IWalletRepository
    {
        Task<NxtAccount> GetAccount(string slackId);
        Task<NxtAccount> AddAccount(NxtAccount account);
        Task Init(Func<string> keyGenerator);
        Task<string> GetMasterKey();
        Task UpdateAccount(NxtAccount account);
    }

    public class WalletRepository : IWalletRepository
    {
        private const string MasterKey = "masterKey";

        public async Task Init(Func<string> keyGenerator)
        {
            using (var context = new WalletContext())
            {
                var masterKey = await GetMasterKey();
                if (string.IsNullOrEmpty(masterKey))
                {
                    masterKey = keyGenerator();
                    context.Settings.Add(new Setting { Key = MasterKey, Value = masterKey });
                    await context.SaveChangesAsync();
                }
            }
        }

        public async Task<string> GetMasterKey()
        {
            using (var context = new WalletContext())
            {
                var masterKey = await context.Settings.SingleOrDefaultAsync(s => s.Key == MasterKey);
                return masterKey?.Value;
            }
        }

        public async Task<NxtAccount> GetAccount(string slackId)
        {
            using (var context = new WalletContext())
            {
                var account = await context.NxtAccounts.SingleOrDefaultAsync(a => a.SlackId == slackId);
                return account;
            }
        }

        public async Task<NxtAccount> AddAccount(NxtAccount account)
        {
            using (var context = new WalletContext())
            {
                context.NxtAccounts.Add(account);
                await context.SaveChangesAsync();
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