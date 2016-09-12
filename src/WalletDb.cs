using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace NxtTipbot
{
    public class WalletDb
    {
        private readonly string filepath;

        public WalletDb(string filepath)
        {
            var folder = Path.GetDirectoryName(filepath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            this.filepath = filepath;
        }

        public async Task<NxtAccount> GetAccount(string slackId)
        {
            using (var context = new WalletContext())
            {
                var account = await context.NxtAccounts.SingleOrDefaultAsync(a => a.SlackId == slackId);
                return account;
            }
        }

        public async Task<NxtAccount> CreateAccount(string slackId, string secretPhrase, string addressRs)
        {
            using (var context = new WalletContext())
            {
                var account = new NxtAccount { SlackId = slackId, SecretPhrase = secretPhrase, NxtAccountRs = addressRs };
                context.NxtAccounts.Add(account);
                await context.SaveChangesAsync();
                return account;
            }
        }
    }
}