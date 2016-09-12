using NxtLib;

namespace NxtTipbot
{
    public class NxtAccount
    {
        private readonly Account account;
        
        public NxtAccount(long id, string slackId, string secretPhrase, string addressRs)
        {
            Id = id;
            SlackId = slackId;
            SecretPhrase = secretPhrase;
            account = new Account(addressRs);
        }

        public long Id { get; }
        public string SlackId { get; }
        public string SecretPhrase { get; }
        public string NxtAccountRs { get { return account.AccountRs; } }
        public ulong NxtAccountId { get { return account.AccountId; } }
    }
}