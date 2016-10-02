using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NxtTipbot.Model;

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