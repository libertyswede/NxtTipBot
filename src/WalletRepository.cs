using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace NxtTipbot
{
    public interface IWalletRepository
    {
        Task<NxtAccount> GetAccount(string slackId);
        Task<NxtAccount> AddAccount(NxtAccount account);
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
    }
}