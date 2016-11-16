using System.Collections.Generic;
using System.Threading.Tasks;
using NxtTipbot.Model;
using NxtLib;
using System.Linq;
using Newtonsoft.Json;

namespace NxtTipbot
{
    public interface IBlockchainStore
    {
        Task BackupAccount(NxtAccount account);
    }

    public class BlockchainStore : IBlockchainStore
    {
        public AccountWithPublicKey MainAccount { get; private set; }
        private readonly INxtConnector nxtConnector;
        private readonly string secretPhrase;
        private const string tagName = "tipbotdata";
        private const string tagVersion = "v1";
        private readonly string tags = string.Join(",", tagName, tagVersion);

        public BlockchainStore(string secretPhrase, INxtConnector nxtConnector)
        {
            this.nxtConnector = nxtConnector;
            this.secretPhrase = secretPhrase;
            MainAccount = nxtConnector.GetAccountFromSecretPhrase(secretPhrase);
        }

        public async Task<int> VerifyBackupStatus(List<NxtAccount> accounts)
        {
            var encryptedMessages = await nxtConnector.SearchTaggedData(MainAccount.AccountRs, tags);
            foreach (var encryptedMessage in encryptedMessages)
            {
                var decrypted = nxtConnector.Decrypt(encryptedMessage, MainAccount, secretPhrase);
                var accountData = JsonConvert.DeserializeObject<NxtAccount>(decrypted);
                accounts.Remove(accounts.Single(a => a.Id == accountData.Id));
            }
            var count = 0;
            foreach (var account in accounts)
            {
                await BackupAccount(account);
                count++;
            }
            return count;
        }

        public async Task BackupAccount(NxtAccount account)
        {
            var decryptedAccountData = JsonConvert.SerializeObject(account);
            var encryptedMessage = nxtConnector.Encrypt(decryptedAccountData, MainAccount, secretPhrase);
            var encryptedJson = JsonConvert.SerializeObject(encryptedMessage);
            await nxtConnector.UploadTaggedData(encryptedJson, tagName, tags, "slack tipbot data storage", secretPhrase);
        }
    }
}
